package com.nadergorge.paymentlistener.data.sim

import com.nadergorge.paymentlistener.data.preference.WalletPairing

data class SimAssignmentClaims(
    val activeWalletTokens: Set<String>,
    val staleWalletTokens: Set<String>
) {
    val isBlocked: Boolean
        get() = activeWalletTokens.isNotEmpty()
}

object SimSubscriptionMapping {
    fun needsExplicitAssignment(
        pairing: WalletPairing,
        activeSubscriptions: List<SimSubscription>
    ): Boolean {
        val subscriptionId = pairing.subscriptionId ?: return true
        val slotIndex = pairing.simSlotIndex ?: return true
        return activeSubscriptions.none { subscription ->
            subscription.subscriptionId == subscriptionId && subscription.slotIndex == slotIndex
        }
    }

    fun isAssigned(
        subscription: SimSubscription,
        pairings: List<WalletPairing>
    ): Boolean = pairings.any { pairing ->
        pairing.subscriptionId == subscription.subscriptionId ||
            pairing.simSlotIndex == subscription.slotIndex
    }

    fun assignmentClaims(
        subscription: SimSubscription,
        pairings: List<WalletPairing>,
        currentWalletToken: String,
        activeSubscriptions: List<SimSubscription>
    ): SimAssignmentClaims {
        val claimingPairings = pairings.filter { pairing ->
            pairing.token != currentWalletToken &&
            (pairing.subscriptionId == subscription.subscriptionId ||
                pairing.simSlotIndex == subscription.slotIndex)
        }
        val claimsByStaleness = claimingPairings.partition { pairing ->
            needsExplicitAssignment(pairing, activeSubscriptions)
        }
        return SimAssignmentClaims(
            activeWalletTokens = claimsByStaleness.second.map(WalletPairing::token).toSet(),
            staleWalletTokens = claimsByStaleness.first.map(WalletPairing::token).toSet()
        )
    }
}
