package com.nadergorge.paymentlistener.data.sim

import com.nadergorge.paymentlistener.data.preference.WalletPairing
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class SimSubscriptionMappingTest {
    @Test
    fun exactSubscriptionAndSlotMatchNeedsNoAssignment() {
        val pairing = wallet(token = "WALLET-A", subscriptionId = 10, slotIndex = 0)

        val needsAssignment = SimSubscriptionMapping.needsExplicitAssignment(
            pairing,
            listOf(subscription(subscriptionId = 10, slotIndex = 0))
        )

        assertFalse(needsAssignment)
    }

    @Test
    fun swappedSubscriptionIdentitiesRequireExplicitAssignment() {
        val firstWallet = wallet(token = "WALLET-A", subscriptionId = 10, slotIndex = 0)
        val secondWallet = wallet(token = "WALLET-B", subscriptionId = 20, slotIndex = 1)
        val swappedSubscriptions = listOf(
            subscription(subscriptionId = 20, slotIndex = 0),
            subscription(subscriptionId = 10, slotIndex = 1)
        )

        assertTrue(SimSubscriptionMapping.needsExplicitAssignment(firstWallet, swappedSubscriptions))
        assertTrue(SimSubscriptionMapping.needsExplicitAssignment(secondWallet, swappedSubscriptions))
    }

    @Test
    fun storedSubscriptionMissingFromPhoneRequiresExplicitAssignment() {
        val pairing = wallet(token = "WALLET-A", subscriptionId = 10, slotIndex = 0)

        assertTrue(SimSubscriptionMapping.needsExplicitAssignment(pairing, emptyList()))
    }

    @Test
    fun incompleteStoredIdentityRequiresExplicitAssignment() {
        val incompletePairings = listOf(
            WalletPairing(token = "WALLET-A", subscriptionId = null, simSlotIndex = 0),
            WalletPairing(token = "WALLET-B", subscriptionId = 20, simSlotIndex = null)
        )
        val activeSubscriptions = listOf(subscription(subscriptionId = 20, slotIndex = 0))

        incompletePairings.forEach { pairing ->
            assertTrue(SimSubscriptionMapping.needsExplicitAssignment(pairing, activeSubscriptions))
        }
    }

    @Test
    fun staleClaimsRemainSelectableAndIdentifyWalletsThatNeedReassignment() {
        val currentWallet = wallet(token = "WALLET-A", subscriptionId = 10, slotIndex = 0)
        val otherWallet = wallet(token = "WALLET-B", subscriptionId = 20, slotIndex = 1)
        val swappedSubscriptions = listOf(
            subscription(subscriptionId = 20, slotIndex = 0),
            subscription(subscriptionId = 10, slotIndex = 1)
        )

        val claims = SimSubscriptionMapping.assignmentClaims(
            subscription = swappedSubscriptions[1],
            pairings = listOf(currentWallet, otherWallet),
            currentWalletToken = currentWallet.token,
            activeSubscriptions = swappedSubscriptions
        )

        assertFalse(claims.isBlocked)
        assertEquals(setOf(otherWallet.token), claims.staleWalletTokens)
    }

    @Test
    fun exactActiveClaimBlocksAssignmentToAnotherWallet() {
        val currentWallet = wallet(token = "WALLET-A", subscriptionId = 10, slotIndex = 0)
        val otherWallet = wallet(token = "WALLET-B", subscriptionId = 20, slotIndex = 1)
        val activeSubscriptions = listOf(
            subscription(subscriptionId = 10, slotIndex = 0),
            subscription(subscriptionId = 20, slotIndex = 1)
        )

        val claims = SimSubscriptionMapping.assignmentClaims(
            subscription = activeSubscriptions[1],
            pairings = listOf(currentWallet, otherWallet),
            currentWalletToken = currentWallet.token,
            activeSubscriptions = activeSubscriptions
        )

        assertTrue(claims.isBlocked)
        assertEquals(setOf(otherWallet.token), claims.activeWalletTokens)
    }

    private fun wallet(token: String, subscriptionId: Int, slotIndex: Int) = WalletPairing(
        token = token,
        subscriptionId = subscriptionId,
        simSlotIndex = slotIndex
    )

    private fun subscription(subscriptionId: Int, slotIndex: Int) = SimSubscription(
        subscriptionId = subscriptionId,
        slotIndex = slotIndex,
        displayName = "SIM ${slotIndex + 1}",
        carrierName = "Carrier"
    )
}
