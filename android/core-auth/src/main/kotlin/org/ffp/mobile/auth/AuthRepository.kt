package org.ffp.mobile.auth

import android.content.Context
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.ffp.mobile.network.LoginRequest
import org.ffp.mobile.network.DeviceRegistration
import org.ffp.mobile.network.DeviceStatus
import org.ffp.mobile.network.MobileApi
import org.ffp.mobile.network.MobileApiFactory
import org.ffp.mobile.network.MobileApplication
import org.ffp.mobile.network.privateEndpoint
import org.ffp.mobile.security.CredentialStore
import org.ffp.mobile.security.InstallationKey
import retrofit2.HttpException
import java.io.IOException
import java.time.Instant
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class DeviceSettings @Inject constructor(@ApplicationContext context: Context) {
    private val preferences = context.getSharedPreferences("ffp_device", Context.MODE_PRIVATE)

    fun endpoint(): String = preferences.getString("api-endpoint", MockSignIn.defaultEndpoint).orEmpty()

    fun saveEndpoint(value: String): Boolean {
        val endpoint = if (value.isBlank()) "" else privateEndpoint(value) ?: return false
        return preferences.edit().putString("api-endpoint", endpoint).commit()
    }
}

data class AccountSession(
    val displayName: String,
    val expiresAt: Instant,
    val authorizedModules: Set<String>,
    val mock: Boolean = false,
)

class AuthenticationFeedback(message: String) : Exception(message)

class AuthRepository @Inject constructor(
    @ApplicationContext private val context: Context,
    private val settings: DeviceSettings,
    private val api: MobileApiFactory,
    private val credentials: CredentialStore,
    private val installation: InstallationKey,
) {
    val mockAvailable: Boolean get() = MockSignIn.available

    suspend fun clearSession() = withContext(Dispatchers.IO) {
        val saved = credentials.accessToken()
        credentials.clear()
        if (saved != null) {
            val (endpoint, token) = saved
            try { signedClient(endpoint).logout("Bearer $token") }
            catch (_: IOException) { }
            catch (_: HttpException) { }
        }
    }

    suspend fun signIn(
        application: MobileApplication, username: String, password: String, mock: Boolean,
    ): AccountSession = withContext(Dispatchers.IO) {
        require(username.isNotBlank() && username.length <= 256)
        require(password.isNotBlank() && password.length <= 1024)
        if (mock) {
            check(mockAvailable)
            return@withContext MockSignIn.authenticate(context, api, application, username.trim(), password)
        }
        val endpoint = configuredOrigin()
        val client = signedClient(endpoint)
        val device = register(client, application)
        if (device.status != 1) throw AuthenticationFeedback(approvalMessage(device))
        val response = client.login(LoginRequest(username.trim(), password, application.apiId))
        val expiry = Instant.parse(requireNotNull(response.expiresAtUtc))
        val modules = response.authorizedModules.orEmpty().filterNot { it.isBlank() }.toSet()
        val token = requireNotNull(response.accessToken)
        check(token.isNotBlank() && modules.isNotEmpty() && expiry.isAfter(Instant.now()))
        val session = AccountSession(requireNotNull(response.displayName), expiry, modules)
        credentials.saveAccessToken(endpoint, token)
        session
    }

    suspend fun enrollTablet(application: MobileApplication): String = withContext(Dispatchers.IO) {
        approvalMessage(register(signedClient(configuredOrigin()), application))
    }

    private fun configuredOrigin(): String = requireNotNull(privateEndpoint(settings.endpoint())).also {
        require(java.net.URI(it).path == "/")
    }

    private fun signedClient(endpoint: String): MobileApi =
        api.create(endpoint, MockSignIn.localTrust(context, endpoint), installation::headers)

    private suspend fun register(client: MobileApi, application: MobileApplication): DeviceStatus {
        val device = try { client.deviceStatus() } catch (failure: HttpException) {
            if (failure.code() != 401) throw failure
            client.register(DeviceRegistration(android.os.Build.MODEL.take(80), application.apiId, installation.publicKey()))
        }
        if (device.application != application.apiId)
            throw AuthenticationFeedback("This installation is registered to another application.")
        return device
    }

    private fun approvalMessage(device: DeviceStatus): String = when (device.status) {
        0 -> "Tablet pending approval. Installation ID: ${device.id}"
        1 -> "Tablet approved."
        2 -> "This tablet is suspended."
        3 -> "This tablet registration has been revoked."
        else -> "Tablet approval status is unknown."
    }
}
