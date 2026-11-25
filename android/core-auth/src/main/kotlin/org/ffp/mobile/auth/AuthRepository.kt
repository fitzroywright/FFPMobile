package org.ffp.mobile.auth

import android.content.Context
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.ffp.mobile.network.LoginRequest
import org.ffp.mobile.network.MobileApiFactory
import org.ffp.mobile.network.MobileApplication
import org.ffp.mobile.network.privateEndpoint
import org.ffp.mobile.security.CredentialStore
import java.time.Instant
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class DeviceSettings @Inject constructor(@ApplicationContext context: Context) {
    private val preferences = context.getSharedPreferences("ffp_device", Context.MODE_PRIVATE)

    fun endpoint(): String = preferences.getString("api-endpoint", "").orEmpty()

    fun saveEndpoint(value: String): Boolean {
        val endpoint = if (value.isBlank()) "" else privateEndpoint(value) ?: return false
        return preferences.edit().putString("api-endpoint", endpoint).commit()
    }
}

data class AccountSession(
    val displayName: String,
    val expiresAt: Instant,
    val authorizedModules: Set<String>,
)

class AuthRepository @Inject constructor(
    private val settings: DeviceSettings,
    private val api: MobileApiFactory,
    private val credentials: CredentialStore,
) {
    // Commission with the approved permission resolver, enrollment and session lifecycle.
    val signInEnabled: Boolean get() = false

    suspend fun clearSession() = withContext(Dispatchers.IO) { credentials.clear() }

    suspend fun signIn(
        application: MobileApplication, username: String, password: String,
    ): AccountSession = withContext(Dispatchers.IO) {
        check(signInEnabled) { "Sign In is unavailable until FFP Manager permissions are commissioned." }
        require(username.isNotBlank() && username.length <= 256)
        require(password.isNotBlank() && password.length <= 1024)
        val endpoint = requireNotNull(privateEndpoint(settings.endpoint()))
        val response = api.create(endpoint).login(LoginRequest(username.trim(), password, application.apiId))
        val expiry = Instant.parse(requireNotNull(response.expiresAtUtc))
        val modules = response.authorizedModules.orEmpty().filterNot { it.isBlank() }.toSet()
        val token = requireNotNull(response.accessToken)
        check(token.isNotBlank() && modules.isNotEmpty() && expiry.isAfter(Instant.now()))
        val session = AccountSession(requireNotNull(response.displayName), expiry, modules)
        credentials.saveAccessToken(token)
        session
    }
}
