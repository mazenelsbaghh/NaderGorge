package com.nadergorge.paymentlistener.service

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class SmsInboxReconciliationPolicyTest {
    @Test
    fun initialCursorStartsFortyEightHoursBeforeNow() {
        val now = 200_000_000L

        val cursor = SmsInboxReconciliationPolicy.initialCursor(now)

        assertEquals(now - SmsInboxReconciliationPolicy.INITIAL_LOOKBACK_MILLIS, cursor.receivedAtMillis)
        assertEquals(-1L, cursor.messageId)
    }

    @Test
    fun initialCursorNeverUsesANegativeInboxTimestamp() {
        val cursor = SmsInboxReconciliationPolicy.initialCursor(1_000L)

        assertEquals(0L, cursor.receivedAtMillis)
        assertEquals(-1L, cursor.messageId)
    }

    @Test
    fun cursorOrdersMessagesByTimestampThenMessageId() {
        val cursor = SmsInboxCursor(receivedAtMillis = 1_000L, messageId = 9L)

        assertFalse(SmsInboxReconciliationPolicy.isAfter(999L, 99L, cursor))
        assertFalse(SmsInboxReconciliationPolicy.isAfter(1_000L, 9L, cursor))
        assertTrue(SmsInboxReconciliationPolicy.isAfter(1_000L, 10L, cursor))
        assertTrue(SmsInboxReconciliationPolicy.isAfter(1_001L, 1L, cursor))
    }

    @Test
    fun latestCursorNeverRegressesWhenWorkersFinishOutOfOrder() {
        val current = SmsInboxCursor(receivedAtMillis = 2_000L, messageId = 20L)

        assertEquals(
            current,
            SmsInboxReconciliationPolicy.latestCursor(
                current,
                SmsInboxCursor(receivedAtMillis = 1_999L, messageId = 99L)
            )
        )
        assertEquals(
            SmsInboxCursor(receivedAtMillis = 2_000L, messageId = 21L),
            SmsInboxReconciliationPolicy.latestCursor(
                current,
                SmsInboxCursor(receivedAtMillis = 2_000L, messageId = 21L)
            )
        )
    }

    @Test
    fun senderFilterAcceptsConfiguredProviderAndRejectsBlankOrUnrelatedSenders() {
        val filters = listOf("VF-Cash", "VodafoneCash")

        assertTrue(SmsInboxReconciliationPolicy.isAllowedSender("vf-cash", filters))
        assertFalse(SmsInboxReconciliationPolicy.isAllowedSender("", filters))
        assertFalse(SmsInboxReconciliationPolicy.isAllowedSender("Unrelated", filters))
    }

    @Test
    fun senderFilterPreservesLegacyReverseContainmentCompatibility() {
        assertTrue(
            SmsInboxReconciliationPolicy.isAllowedSender(
                sender = "VF-Cash",
                filters = listOf("Wallet-VF-Cash-Notifications")
            )
        )
    }

    @Test
    fun equivalentFiltersIgnoreOrderCaseWhitespaceAndDuplicates() {
        assertTrue(
            SmsInboxReconciliationPolicy.haveEquivalentFilters(
                current = listOf("VF-Cash", "OrangeCash"),
                candidate = listOf(" orangecash ", "vf-cash", "VF-CASH")
            )
        )
    }

    @Test
    fun differentProviderFilterRequiresCursorReset() {
        assertFalse(
            SmsInboxReconciliationPolicy.haveEquivalentFilters(
                current = listOf("VF-Cash"),
                candidate = listOf("EtisalatCash")
            )
        )
    }

    @Test
    fun receivedAtFormattingPreservesUtcInstant() {
        assertEquals(
            "2026-08-25T16:04:00Z",
            SmsInboxReconciliationPolicy.formatReceivedAt(1_787_673_840_000L)
        )
    }
}
