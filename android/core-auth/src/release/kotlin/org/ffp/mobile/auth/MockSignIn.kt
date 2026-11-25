package org.ffp.mobile.auth

import android.content.Context
import org.ffp.mobile.network.MobileApiFactory
import org.ffp.mobile.network.MobileApplication
import javax.net.ssl.X509TrustManager

internal object MockSignIn {
    const val available = false
    const val defaultEndpoint = "https://mobile.ffpja.org/"

    @Suppress("UNUSED_PARAMETER")
    fun localTrust(context: Context, endpoint: String): X509TrustManager? = null

    @Suppress("UNUSED_PARAMETER")
    suspend fun authenticate(
        context: Context, api: MobileApiFactory, application: MobileApplication, username: String, password: String,
    ): AccountSession =
        error("Mock authentication is disabled.")
}
