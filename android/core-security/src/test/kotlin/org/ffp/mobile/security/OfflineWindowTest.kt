package org.ffp.mobile.security

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import java.time.Instant

class OfflineWindowTest {
    private val issued = Instant.parse("2026-01-01T00:00:00Z")
    private val expiry = issued.plusSeconds(3600)

    @Test fun acceptsOnlyCurrentBoundedWindow() {
        assertTrue(validOfflineWindow(issued, expiry, issued))
        assertFalse(validOfflineWindow(issued, expiry, issued.minusSeconds(1)))
        assertFalse(validOfflineWindow(issued, expiry, expiry))
        assertFalse(validOfflineWindow(issued, issued.plusSeconds(29_000), issued))
    }
}
