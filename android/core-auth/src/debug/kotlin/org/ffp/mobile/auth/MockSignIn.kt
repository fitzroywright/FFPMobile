package org.ffp.mobile.auth

import android.content.Context
import org.ffp.mobile.network.LoginRequest
import org.ffp.mobile.network.MobileApiFactory
import org.ffp.mobile.network.MobileApplication
import retrofit2.HttpException
import java.io.FileNotFoundException
import java.security.KeyStore
import java.security.cert.CertificateFactory
import java.time.Instant
import javax.net.ssl.TrustManagerFactory
import javax.net.ssl.X509TrustManager

internal object MockSignIn {
    const val available = true
    const val defaultEndpoint = "https://localhost:7241/"

    fun localTrust(context: Context, endpoint: String): X509TrustManager? {
        if (endpoint != defaultEndpoint) return null
        val certificate = try {
            context.assets.open("warehouse-mock.cer").use { CertificateFactory.getInstance("X.509").generateCertificate(it) }
        } catch (_: FileNotFoundException) {
            throw AuthenticationFeedback("The local helper certificate is missing from this debug build.")
        }
        val anchors = KeyStore.getInstance(KeyStore.getDefaultType()).apply {
            load(null)
            setCertificateEntry("warehouse-mock", certificate)
        }
        return TrustManagerFactory.getInstance(TrustManagerFactory.getDefaultAlgorithm())
            .apply { init(anchors) }.trustManagers.filterIsInstance<X509TrustManager>().single()
    }

    suspend fun authenticate(
        context: Context, api: MobileApiFactory, application: MobileApplication, username: String, password: String,
    ): AccountSession {
        val response = try {
            api.create(defaultEndpoint, localTrust(context, defaultEndpoint))
                .mockLogin(LoginRequest(username, password, application.apiId))
        } catch (failure: HttpException) {
            if (failure.code() == 401) throw AuthenticationFeedback("Use ${application.demoLogin} and demo for Mock sign-in.")
            throw failure
        }
        return AccountSession(requireNotNull(response.displayName), Instant.parse(response.expiresAtUtc),
            response.authorizedModules.orEmpty().toSet(), mock = true)
    }
}
