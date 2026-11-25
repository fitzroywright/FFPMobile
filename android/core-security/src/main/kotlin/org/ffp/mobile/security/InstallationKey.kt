package org.ffp.mobile.security

import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.util.Base64
import java.security.KeyPairGenerator
import java.security.KeyStore
import java.security.MessageDigest
import java.security.PrivateKey
import java.security.Signature
import java.security.spec.ECGenParameterSpec
import java.time.Instant
import java.util.UUID
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class InstallationKey @Inject constructor() {
    @Synchronized
    private fun store(): KeyStore {
        val store = KeyStore.getInstance("AndroidKeyStore").apply { load(null) }
        if (!store.containsAlias(ALIAS)) {
            KeyPairGenerator.getInstance("EC", "AndroidKeyStore").apply {
                initialize(KeyGenParameterSpec.Builder(ALIAS, KeyProperties.PURPOSE_SIGN)
                    .setAlgorithmParameterSpec(ECGenParameterSpec("secp256r1"))
                    .setDigests(KeyProperties.DIGEST_SHA256).build())
                generateKeyPair()
            }
        }
        return store
    }

    fun publicKey(): String = Base64.encodeToString(store().getCertificate(ALIAS).publicKey.encoded, Base64.NO_WRAP)

    fun headers(method: String, path: String, token: String): Map<String, String> {
        val store = store()
        val id = digest(store.getCertificate(ALIAS).publicKey.encoded)
        val nonce = UUID.randomUUID().toString().replace("-", "")
        val time = Instant.now().epochSecond.toString()
        val message = listOf("ffpmobile-device-v1", method, path, id, nonce, time, digest(token.toByteArray()))
            .joinToString("\n").toByteArray()
        val proof = Signature.getInstance("SHA256withECDSA").run {
            initSign(store.getKey(ALIAS, null) as PrivateKey)
            update(message)
            Base64.encodeToString(sign(), Base64.NO_WRAP)
        }
        return mapOf("X-FFP-Installation" to id, "X-FFP-Nonce" to nonce,
            "X-FFP-Time" to time, "X-FFP-Signature" to proof)
    }

    private fun digest(bytes: ByteArray): String =
        MessageDigest.getInstance("SHA-256").digest(bytes).joinToString("") { "%02X".format(it) }

    private companion object {
        const val ALIAS = "ffpmobile.installation.v1"
    }
}
