package org.ffp.mobile.network

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class PrivateEndpointTest {
    @Test fun normalizesHttpsAndPreservesBasePath() {
        assertEquals("https://mobile.example.test/", privateEndpoint(" https://mobile.example.test "))
        assertEquals("https://mobile.example.test/mobile/", privateEndpoint("https://mobile.example.test/mobile"))
    }

    @Test fun rejectsInsecureOrCredentialBearingEndpoints() {
        listOf(
            "", "http://mobile.example.test", "https://user:password@mobile.example.test",
            "https://@mobile.example.test", "https://mobile.example.test/?secret=1",
            "https://mobile.example.test/#fragment", "https:///mobile.example.test",
        ).forEach { endpoint -> assertNull(endpoint, privateEndpoint(endpoint)) }
    }
}
