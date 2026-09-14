package com.nadergorge.paymentlistener.service

import com.nadergorge.paymentlistener.data.preference.WalletPairing

sealed interface WalletRoutingResult {
    data class Routed(val pairing: WalletPairing) : WalletRoutingResult
    data object NoWallets : WalletRoutingResult
    data object NoMatch : WalletRoutingResult
    data object Ambiguous : WalletRoutingResult
}

object WalletRoutingPolicy {
    fun route(
        pairings: List<WalletPairing>,
        subscriptionId: Int?,
        simSlotIndex: Int?
    ): WalletRoutingResult {
        if (pairings.isEmpty()) return WalletRoutingResult.NoWallets
        val routablePairings = pairings.filter { pairing ->
            pairing.subscriptionId != null && pairing.simSlotIndex != null
        }
        if (routablePairings.isEmpty()) return WalletRoutingResult.NoMatch
        if (subscriptionId == null) {
            return if (simSlotIndex == null && routablePairings.size > 1) {
                WalletRoutingResult.Ambiguous
            } else {
                WalletRoutingResult.NoMatch
            }
        }

        val exactSubscriptionMatches = routablePairings.filter { pairing ->
            pairing.subscriptionId == subscriptionId
        }
        if (exactSubscriptionMatches.isNotEmpty()) {
            val slotCompatibleMatches = exactSubscriptionMatches.filter { pairing ->
                simSlotIndex == null ||
                    pairing.simSlotIndex == simSlotIndex
            }
            return if (slotCompatibleMatches.isEmpty()) {
                WalletRoutingResult.NoMatch
            } else {
                uniqueRoute(slotCompatibleMatches)
            }
        }

        return WalletRoutingResult.NoMatch
    }

    fun isSenderAllowed(pairing: WalletPairing, sender: String?): Boolean =
        SmsInboxReconciliationPolicy.isAllowedSender(sender, pairing.smsSenderFilters)

    private fun uniqueRoute(matches: List<WalletPairing>): WalletRoutingResult =
        if (matches.size == 1) {
            WalletRoutingResult.Routed(matches.single())
        } else {
            WalletRoutingResult.Ambiguous
        }
}
