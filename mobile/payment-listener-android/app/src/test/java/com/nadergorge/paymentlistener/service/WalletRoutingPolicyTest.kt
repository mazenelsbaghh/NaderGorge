package com.nadergorge.paymentlistener.service

import com.nadergorge.paymentlistener.data.preference.WalletPairing
import org.junit.Assert.assertEquals
import org.junit.Assert.assertSame
import org.junit.Test

class WalletRoutingPolicyTest {
    @Test
    fun knownInboxSubscriptionMismatchDoesNotRouteSoleWallet() {
        val pairing = wallet(token = "WALLET-A", subscriptionId = 10, simSlotIndex = 0)

        val result = WalletRoutingPolicy.route(
            pairings = listOf(pairing),
            subscriptionId = 20,
            simSlotIndex = null
        )

        assertSame(WalletRoutingResult.NoMatch, result)
    }

    @Test
    fun conflictingSubscriptionAndSlotMetadataFailsClosed() {
        val pairing = wallet(token = "WALLET-A", subscriptionId = 10, simSlotIndex = 0)

        val result = WalletRoutingPolicy.route(
            pairings = listOf(pairing),
            subscriptionId = 20,
            simSlotIndex = 0
        )

        assertSame(WalletRoutingResult.NoMatch, result)
    }

    @Test
    fun exactSubscriptionConflictDoesNotFallBackToAnotherWalletSlot() {
        val slotOnlyWallet = WalletPairing(
            token = "WALLET-A",
            subscriptionId = null,
            simSlotIndex = 0
        )
        val subscriptionWallet = wallet(
            token = "WALLET-B",
            subscriptionId = 20,
            simSlotIndex = 1
        )

        val result = WalletRoutingPolicy.route(
            pairings = listOf(slotOnlyWallet, subscriptionWallet),
            subscriptionId = 20,
            simSlotIndex = 0
        )

        assertSame(WalletRoutingResult.NoMatch, result)
    }

    @Test
    fun exactSubscriptionRoutesOnlyItsWallet() {
        val first = wallet(token = "WALLET-A", subscriptionId = 10, simSlotIndex = 0)
        val second = wallet(token = "WALLET-B", subscriptionId = 20, simSlotIndex = 1)

        val result = WalletRoutingPolicy.route(
            pairings = listOf(first, second),
            subscriptionId = 20,
            simSlotIndex = 1
        )

        assertEquals(WalletRoutingResult.Routed(second), result)
    }

    @Test
    fun missingIdentityWithTwoWalletsIsAmbiguous() {
        val result = WalletRoutingPolicy.route(
            pairings = listOf(
                wallet(token = "WALLET-A", subscriptionId = 10, simSlotIndex = 0),
                wallet(token = "WALLET-B", subscriptionId = 20, simSlotIndex = 1)
            ),
            subscriptionId = null,
            simSlotIndex = null
        )

        assertSame(WalletRoutingResult.Ambiguous, result)
    }

    @Test
    fun partiallyAssignedWalletsNeverReceiveSmsRoutes() {
        val partialPairings = listOf(
            WalletPairing(token = "SUB-ONLY", subscriptionId = 10, simSlotIndex = null),
            WalletPairing(token = "SLOT-ONLY", subscriptionId = null, simSlotIndex = 0)
        )

        partialPairings.forEach { pairing ->
            val result = WalletRoutingPolicy.route(
                pairings = listOf(pairing),
                subscriptionId = 10,
                simSlotIndex = 0
            )
            assertSame(WalletRoutingResult.NoMatch, result)
        }
    }

    @Test
    fun slotIdentityAloneNeverRoutesAStoredWallet() {
        val result = WalletRoutingPolicy.route(
            pairings = listOf(
                wallet(token = "WALLET-A", subscriptionId = 10, simSlotIndex = 0)
            ),
            subscriptionId = null,
            simSlotIndex = 0
        )

        assertSame(WalletRoutingResult.NoMatch, result)
    }

    @Test
    fun slotResolvedToReplacementSubscriptionDoesNotRouteOldWallet() {
        val pairing = wallet(token = "WALLET-A", subscriptionId = 10, simSlotIndex = 0)
        val replacementIdentity = requireNotNull(
            resolveIncomingSmsSimIdentity(
                broadcastSubscriptionId = null,
                broadcastSimSlotIndex = 0,
                slotsBySubscriptionId = mapOf(20 to 0)
            )
        )

        val result = WalletRoutingPolicy.route(
            pairings = listOf(pairing),
            subscriptionId = replacementIdentity.subscriptionId,
            simSlotIndex = replacementIdentity.simSlotIndex
        )

        assertSame(WalletRoutingResult.NoMatch, result)
    }

    private fun wallet(
        token: String,
        subscriptionId: Int,
        simSlotIndex: Int
    ): WalletPairing = WalletPairing(
        token = token,
        subscriptionId = subscriptionId,
        simSlotIndex = simSlotIndex,
        smsSenderFilters = listOf("VF-Cash")
    )
}
