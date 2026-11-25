package org.ffp.mobile.security

import org.json.JSONObject
import java.security.interfaces.RSAPublicKey
import java.security.Signature
import java.time.Duration
import java.time.Instant
import javax.inject.Inject

data class AuthorizationScope(val application: String, val device: String, val account: String)

/** A cached signed grant has no effect until enrollment supplies a trusted key and clock. */
class OfflineAuthorizationCache @Inject constructor(private val vault: KeystoreVault) {
    fun save(
        document: ByteArray,
        signature: ByteArray,
        trustedKey: RSAPublicKey,
        scope: AuthorizationScope,
        now: Instant,
    ): Boolean {
        val envelope = document + signature
        if (validate(document, signature, trustedKey, scope, now) == null) return false
        vault.write("offline-grant-document", envelope)
        return true
    }

    fun capabilities(
        trustedKey: RSAPublicKey,
        scope: AuthorizationScope,
        now: Instant,
    ): Set<String> {
        val envelope = runCatching { vault.read("offline-grant-document") }.getOrNull() ?: return emptySet()
        val signatureBytes = (trustedKey.modulus.bitLength() + 7) / 8
        if (signatureBytes !in 1 until envelope.size) return emptySet()
        val split = envelope.size - signatureBytes
        return validate(
            envelope.copyOfRange(0, split), envelope.copyOfRange(split, envelope.size),
            trustedKey, scope, now,
        ).orEmpty()
    }

    fun clear() = vault.remove("offline-grant-document")

    private fun validate(
        document: ByteArray,
        signature: ByteArray,
        key: RSAPublicKey,
        scope: AuthorizationScope,
        now: Instant,
    ): Set<String>? = runCatching {
        val valid = Signature.getInstance("SHA256withRSA").run {
            initVerify(key)
            update(document)
            verify(signature)
        }
        if (!valid) return null
        val claims = JSONObject(document.decodeToString())
        if (claims.getString("application") != scope.application ||
            claims.getString("device") != scope.device || claims.getString("account") != scope.account
        ) return null
        val issuedAt = Instant.parse(claims.getString("issuedAt"))
        val expiresAt = Instant.parse(claims.getString("expiresAt"))
        if (!validOfflineWindow(issuedAt, expiresAt, now)) return null
        val capabilities = claims.getJSONArray("capabilities")
        (0 until capabilities.length()).map { capabilities.getString(it) }.toSet()
    }.getOrNull()
}

internal fun validOfflineWindow(issued: Instant, expires: Instant, now: Instant): Boolean =
    now >= issued && now < expires && Duration.between(issued, expires) <= Duration.ofHours(8)
