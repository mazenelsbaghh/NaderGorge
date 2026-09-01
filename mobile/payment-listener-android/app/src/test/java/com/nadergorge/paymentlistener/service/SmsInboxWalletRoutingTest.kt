package com.nadergorge.paymentlistener.service

import com.nadergorge.paymentlistener.data.preference.WalletPairing
import org.junit.Assert.assertSame
import org.junit.Test

class SmsInboxWalletRoutingTest {
    @Test
    fun missingInboxSubscriptionIsAmbiguous() {
        val result = routeInboxWallet(
            pairings = listOf(
                WalletPairing(token = "TOKEN-A", subscriptionId = 10, simSlotIndex = 0)
            ),
            subscriptionId = null
        )

        assertSame(WalletRoutingResult.Ambiguous, result)
    }

    @Test
    fun knownMismatchedSubIdDoesNotRouteToSolePairing() {
        val result = routeInboxWallet(
            pairings = listOf(
                WalletPairing(token = "TOKEN-A", subscriptionId = 10, simSlotIndex = 0)
            ),
            subscriptionId = 20
        )

        assertSame(WalletRoutingResult.NoMatch, result)
    }

    @Test
    fun knownInboxSubscriptionRoutesWhenNoSimIsCurrentlyActive() {
        val expected = WalletPairing(token = "TOKEN-A", subscriptionId = 10, simSlotIndex = 0)

        val result = routeInboxWallet(
            pairings = listOf(expected),
            subscriptionId = 10
        )

        assertSame(expected, (result as WalletRoutingResult.Routed).pairing)
    }

    @Test
    fun missingHistoricalIdentityWithTwoPairingsNeverUsesCurrentSoleActiveSim() {
        val result = routeInboxWallet(
            pairings = listOf(
                WalletPairing(token = "TOKEN-A", subscriptionId = 10, simSlotIndex = 0),
                WalletPairing(token = "TOKEN-B", subscriptionId = 20, simSlotIndex = 1)
            ),
            subscriptionId = null
        )

        assertSame(WalletRoutingResult.Ambiguous, result)
    }

}
