package org.ffp.mobile.network

import okhttp3.HttpUrl.Companion.toHttpUrlOrNull
import okhttp3.OkHttpClient
import retrofit2.Retrofit
import retrofit2.converter.gson.GsonConverterFactory
import retrofit2.http.Body
import retrofit2.http.POST
import java.util.concurrent.TimeUnit
import javax.inject.Inject
import javax.inject.Singleton

enum class MobileApplication(val apiId: String) {
    FIELD("field-officers"), WAREHOUSE("warehouse-operations"), SECURITY("security-post"),
}

class LoginRequest(val userName: String, val password: String, val application: String)

class LoginResponse(
    val accessToken: String?,
    val displayName: String?,
    val expiresAtUtc: String?,
    val authorizedModules: List<String>?,
)

interface MobileApi {
    @POST("api/auth/login")
    suspend fun login(@Body request: LoginRequest): LoginResponse
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

    fun create(endpoint: String): MobileApi = Retrofit.Builder()
        .baseUrl(requireNotNull(privateEndpoint(endpoint)))
        .client(client)
        .addConverterFactory(GsonConverterFactory.create())
        .build()
        .create(MobileApi::class.java)
}
