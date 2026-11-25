package org.ffp.mobile.sync

import org.ffp.mobile.database.OutboxEntry
import org.ffp.mobile.database.TransactionKind
import org.ffp.mobile.database.TransactionStage
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class ReceiptTest {
    @Test fun requiresMatchingIdAndNewerNonRegressiveServerReceipt() {
        val entry = OutboxEntry.warehouse("test-owner", TransactionKind.DELIVERY_ORDER, "{}")
            .copy(stage = TransactionStage.ACCEPTED, serverRevision = 4)
        val receipt = ServerReceipt(entry.id, 5, TransactionStage.APPROVED)
        assertTrue(receipt.canApplyTo(entry))
        assertFalse(receipt.copy(transactionId = "wrong").canApplyTo(entry))
        assertFalse(receipt.copy(revision = 4).canApplyTo(entry))
        assertFalse(receipt.copy(stage = TransactionStage.SERVER_RECEIVED).canApplyTo(entry))
    }
}
