package org.ffp.mobile.security

import android.content.Context
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.util.Base64
import dagger.hilt.android.qualifiers.ApplicationContext
import java.security.KeyStore
import java.security.SecureRandom
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class KeystoreVault @Inject constructor(@ApplicationContext context: Context) {
    private val preferences = context.getSharedPreferences("ffp_vault", Context.MODE_PRIVATE)

    @Synchronized
    internal fun read(name: String): ByteArray? {
        val value = preferences.getString(name, null) ?: return null
        val bytes = Base64.decode(value, Base64.NO_WRAP)
        val cipher = Cipher.getInstance(TRANSFORMATION)
        cipher.init(Cipher.DECRYPT_MODE, key(create = false), GCMParameterSpec(128, bytes, 0, IV_BYTES))
        cipher.updateAAD(name.toByteArray())
        return cipher.doFinal(bytes, IV_BYTES, bytes.size - IV_BYTES)
    }

    @Synchronized
    internal fun write(name: String, value: ByteArray) {
        val cipher = Cipher.getInstance(TRANSFORMATION)
        cipher.init(Cipher.ENCRYPT_MODE, key(create = true))
        cipher.updateAAD(name.toByteArray())
        val encoded = Base64.encodeToString(cipher.iv + cipher.doFinal(value), Base64.NO_WRAP)
        check(preferences.edit().putString(name, encoded).commit())
    }

    @Synchronized
    internal fun remove(name: String) {
        check(preferences.edit().remove(name).commit())
    }

    @Synchronized
    fun databasePassphrase(): ByteArray {
        read(DATABASE_KEY)?.let { return it }
        return ByteArray(32).also {
            SecureRandom().nextBytes(it)
            write(DATABASE_KEY, it)
        }
    }

    private fun key(create: Boolean): SecretKey {
        val store = KeyStore.getInstance("AndroidKeyStore").apply { load(null) }
        (store.getKey(KEY_ALIAS, null) as? SecretKey)?.let { return it }
        check(create) { "FFP storage key is unavailable" }
        val spec = KeyGenParameterSpec.Builder(
            KEY_ALIAS, KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT,
        ).setBlockModes(KeyProperties.BLOCK_MODE_GCM)
            .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
            .setKeySize(256)
            .build()
        return KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore").run {
            init(spec)
            generateKey()
        }
    }

    private companion object {
        const val KEY_ALIAS = "ffp.vault.v1"
        const val DATABASE_KEY = "database-key"
        const val TRANSFORMATION = "AES/GCM/NoPadding"
        const val IV_BYTES = 12
    }
}

class CredentialStore @Inject constructor(private val vault: KeystoreVault) {
    fun saveAccessToken(token: String) = vault.write("api-access-token", token.toByteArray())
    fun clear() = vault.remove("api-access-token")
}
