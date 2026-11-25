package org.ffp.mobile.database

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import java.util.UUID

class OutboxTest {
    @Test fun localEntriesHaveDistinctStableIdsAndNoReceipt() {
        val first = OutboxEntry.warehouse("test-owner", TransactionKind.INVENTORY_COUNT, "{}")
        val second = OutboxEntry.warehouse("test-owner", TransactionKind.INVENTORY_COUNT, "{}")
        assertEquals(first.id, UUID.fromString(first.id).toString())
        assertEquals(first.id, first.copy().id)
        assertNotEquals(first.id, second.id)
        assertEquals(TransactionStage.LOCALLY_SAVED, first.stage)
        assertEquals(0L, first.serverRevision)
    }

    @Test fun serverStagesCannotRegressOrClearTerminalOutcomes() {
        assertTrue(TransactionStage.SERVER_RECEIVED.permits(TransactionStage.ACCEPTED))
        assertTrue(TransactionStage.ACCEPTED.permits(TransactionStage.CONFLICT))
        assertFalse(TransactionStage.APPROVED.permits(TransactionStage.ACCEPTED))
        assertFalse(TransactionStage.CONFLICT.permits(TransactionStage.POSTED))
        assertFalse(TransactionStage.REJECTED.permits(TransactionStage.LOCALLY_SAVED))
    }
}
