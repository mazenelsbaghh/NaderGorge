package com.nadergorge.paymentlistener.service

import com.nadergorge.paymentlistener.data.preference.WalletPairing
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Test

class WalletStatusSyncSummaryTest {
    @Test
    fun summaryPreservesSuccessAlongsideConfigurationFailure() {
        val summary = WalletStatusSyncSummary.from(
            listOf(
                WalletStatusSyncOutcome.CONFIGURATION_FAILURE,
                WalletStatusSyncOutcome.SUCCESS
            )
        )

        assertEquals(2, summary.attemptedCount)
        assertEquals(1, summary.successfulCount)
        assertEquals(0, summary.retryableFailureCount)
        assertEquals(1, summary.configurationFailureCount)
    }

    @Test
    fun everyWalletTokenIsSynchronizedIndependently() = runBlocking {
        val pairings = listOf(
            WalletPairing(token = "TOKEN-A", subscriptionId = 10, simSlotIndex = 0),
            WalletPairing(token = "TOKEN-B", subscriptionId = 20, simSlotIndex = 1)
        )
        val synchronizedTokens = mutableSetOf<String>()

        val outcomes = syncEachWallet(pairings) { pairing ->
            synchronized(synchronizedTokens) { synchronizedTokens += pairing.token }
            WalletStatusSyncOutcome.SUCCESS
        }

        assertEquals(setOf("TOKEN-A", "TOKEN-B"), synchronizedTokens)
        assertEquals(2, outcomes.size)
    }
}
