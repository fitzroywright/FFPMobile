package org.ffp.mobile.database

import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import org.ffp.mobile.security.KeystoreVault
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class EncryptedOutboxTest {
    @Test fun survivesReopenAndPreservesAccountScopeAndConflicts() = runBlocking {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val databaseFile = "ffp-outbox.db"
        context.deleteDatabase(databaseFile)
        val entry = OutboxEntry.warehouse("test-account", TransactionKind.INVENTORY_COUNT, "{}")

        try {
            val initialDatabase = MobileDatabase.open(context, KeystoreVault(context))
            try {
                initialDatabase.outbox().insert(entry)
            } finally {
                initialDatabase.close()
            }
            val header = context.getDatabasePath(databaseFile).inputStream().use {
                ByteArray(16).apply { check(it.read(this) == size) }
            }
            assertNotEquals("SQLite format 3\u0000", header.decodeToString())
            val reopenedDatabase = MobileDatabase.open(context, KeystoreVault(context))
            try {
                val dao = reopenedDatabase.outbox()
                assertEquals(entry, dao.observe(entry.applicationId, entry.accountId).first().single())
                assertTrue(dao.observe(entry.applicationId, "another-account").first().isEmpty())
                val pending = dao.pending(entry.applicationId, entry.accountId, Long.MIN_VALUE, "", 10)
                assertEquals(listOf(entry.id), pending.map { it.id })
                assertEquals(1, dao.reconcile(entry.id, 0, TransactionStage.CONFLICT, 1, "Count changed"))
                assertEquals(0, dao.reconcile(entry.id, 0, TransactionStage.ACCEPTED, 2, null))
                val conflict = dao.observe(entry.applicationId, entry.accountId).first().single()
                assertEquals(TransactionStage.CONFLICT, conflict.stage)
                assertEquals("Count changed", conflict.issue)
            } finally {
                reopenedDatabase.close()
            }
        } finally {
            context.deleteDatabase(databaseFile)
        }
    }
}
