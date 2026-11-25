package org.ffp.mobile.network

import okhttp3.HttpUrl.Companion.toHttpUrlOrNull
import okhttp3.OkHttpClient
import retrofit2.Retrofit
import retrofit2.converter.gson.GsonConverterFactory
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.Header
import retrofit2.http.POST
import java.util.concurrent.TimeUnit
import javax.net.ssl.SSLContext
import javax.net.ssl.X509TrustManager
import javax.inject.Inject
import javax.inject.Singleton

enum class MobileApplication(val apiId: String, val demoLogin: String) {
    FIELD("field-officers", "field-demo"),
    WAREHOUSE("warehouse-operations", "warehouse-demo"),
    SECURITY("security-post", "security-demo"),
}

class LoginRequest(val userName: String, val password: String, val application: String)

class LoginResponse(
    val accessToken: String?,
    val displayName: String?,
    val expiresAtUtc: String?,
    val authorizedModules: List<String>?,
)

class DeviceRegistration(val name: String, val application: String, val publicKey: String)
class DeviceStatus(val id: String, val application: String, val status: Int)

interface MobileApi {
    @GET("api/devices/status")
    suspend fun deviceStatus(): DeviceStatus

    @POST("api/devices/register")
    suspend fun register(@Body request: DeviceRegistration): DeviceStatus

    @POST("api/auth/login")
    suspend fun login(@Body request: LoginRequest): LoginResponse

    @POST("api/auth/mock/login")
    suspend fun mockLogin(@Body request: LoginRequest): LoginResponse

    @POST("api/auth/logout")
    suspend fun logout(@Header("Authorization") authorization: String)
}

fun privateEndpoint(input: String): String? {
    val value = input.trim()
    if (!value.startsWith("https://", ignoreCase = true) || '\\' in value) return null
    val authority = value.substringAfter("://").substringBefore('/').substringBefore('?').substringBefore('#')
    if (authority.isBlank() || '@' in authority) return null
    val url = value.toHttpUrlOrNull() ?: return null
    if (url.query != null || url.fragment != null) return null
    return if (url.encodedPath.endsWith('/')) url.toString() else "$url/"
}

@Singleton
class MobileApiFactory @Inject constructor() {
    private val client = OkHttpClient.Builder()
        .followRedirects(false)
        .followSslRedirects(false)
        .callTimeout(20, TimeUnit.SECONDS)
        .build()

    fun create(
        endpoint: String,
        trust: X509TrustManager? = null,
        proof: ((String, String, String) -> Map<String, String>)? = null,
    ): MobileApi {
        val transport = client.newBuilder()
        if (trust != null) {
            val tls = SSLContext.getInstance("TLS").apply { init(null, arrayOf(trust), null) }
            transport.sslSocketFactory(tls.socketFactory, trust)
        }
        if (proof != null) transport.addInterceptor { chain ->
            val request = chain.request()
            val token = request.header("Authorization").orEmpty().removePrefix("Bearer ")
            val signed = request.newBuilder()
            proof(request.method, request.url.encodedPath, token).forEach { (name, value) -> signed.header(name, value) }
            chain.proceed(signed.build())
        }
        return Retrofit.Builder().baseUrl(requireNotNull(privateEndpoint(endpoint)))
            .client(transport.build())
            .addConverterFactory(GsonConverterFactory.create())
            .build().create(MobileApi::class.java)
    }
}
