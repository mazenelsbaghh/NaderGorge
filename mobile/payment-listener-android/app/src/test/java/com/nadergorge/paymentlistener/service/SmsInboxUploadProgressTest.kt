package com.nadergorge.paymentlistener.service

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import kotlinx.coroutines.runBlocking

class SmsInboxPendingPolicyTest {
    @Test
    fun ambiguousFinancialSmsIsRememberedForExplicitRebinding() {
        val mutation = pendingSmsMutation(
            routing = WalletRoutingResult.Ambiguous,
            senderRelevantToAnyWallet = true,
            senderAllowedForRoutedWallet = false
        )

        assertEquals(PendingSmsMutation.REMEMBER, mutation)
    }

    @Test
    fun routedFinancialSmsStaysPendingUntilItsIndependentWorkerSucceeds() {
        val routing = WalletRoutingResult.Routed(
            com.nadergorge.paymentlistener.data.preference.WalletPairing(
                token = "TOKEN-B",
                subscriptionId = 20,
                simSlotIndex = 1
            )
        )

        val mutation = pendingSmsMutation(
            routing = routing,
            senderRelevantToAnyWallet = true,
            senderAllowedForRoutedWallet = true
        )

        assertEquals(PendingSmsMutation.REMEMBER, mutation)
    }

    @Test
    fun irrelevantSmsClearsAnyStalePendingMarker() {
        val mutation = pendingSmsMutation(
            routing = WalletRoutingResult.NoMatch,
            senderRelevantToAnyWallet = false,
            senderAllowedForRoutedWallet = false
        )

        assertEquals(PendingSmsMutation.CLEAR, mutation)
    }

    @Test
    fun cursorAdvancesOnlyAfterPendingWriteAndDurableEnqueue() = runBlocking {
        val events = mutableListOf<String>()

        val completed = persistEnqueueAndAdvanceInboxSms(
            persistPending = { events += "pending"; true },
            enqueueUpload = { events += "enqueue"; true },
            advanceCursor = { events += "cursor"; true }
        )

        assertTrue(completed)
        assertEquals(listOf("pending", "enqueue", "cursor"), events)
    }

    @Test
    fun failedEnqueueLeavesCursorUnchangedForReplay() = runBlocking {
        var cursorAdvanced = false

        val completed = persistEnqueueAndAdvanceInboxSms(
            persistPending = { true },
            enqueueUpload = { false },
            advanceCursor = { cursorAdvanced = true; true }
        )

        assertFalse(completed)
        assertFalse(cursorAdvanced)
    }

    @Test
    fun workIdentityDeduplicatesSameBindingButChangesAfterRebind() {
        val cursor = SmsInboxCursor(receivedAtMillis = 1_777_777L, messageId = 42L)
        val firstName = inboxUploadWorkName(cursor, "binding-a")

        assertEquals(firstName, inboxUploadWorkName(cursor, "binding-a"))
        assertNotEquals(firstName, inboxUploadWorkName(cursor, "binding-b"))
    }
}
