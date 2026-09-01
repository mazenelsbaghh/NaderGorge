package com.nadergorge.paymentlistener.service

import java.time.Instant
import java.util.Locale

data class SmsInboxCursor(
    val receivedAtMillis: Long,
    val messageId: Long
)

data class SmsInboxReconciliationState(
    val cursor: SmsInboxCursor,
    val filterRevision: Long,
    val senderFilters: List<String>
)

object SmsInboxReconciliationPolicy {
    const val INITIAL_LOOKBACK_MILLIS: Long = 48L * 60L * 60L * 1000L

    fun initialCursor(nowMillis: Long): SmsInboxCursor =
        SmsInboxCursor(
            receivedAtMillis = (nowMillis - INITIAL_LOOKBACK_MILLIS).coerceAtLeast(0L),
            messageId = -1L
        )

    fun isAfter(candidateReceivedAtMillis: Long, candidateMessageId: Long, cursor: SmsInboxCursor): Boolean =
        candidateReceivedAtMillis > cursor.receivedAtMillis ||
            (candidateReceivedAtMillis == cursor.receivedAtMillis && candidateMessageId > cursor.messageId)

    fun latestCursor(current: SmsInboxCursor, candidate: SmsInboxCursor): SmsInboxCursor =
        if (isAfter(candidate.receivedAtMillis, candidate.messageId, current)) candidate else current

    fun earliestCursor(first: SmsInboxCursor, second: SmsInboxCursor): SmsInboxCursor =
        if (isAfter(first.receivedAtMillis, first.messageId, second)) second else first

    fun replayCursorBefore(candidate: SmsInboxCursor): SmsInboxCursor = SmsInboxCursor(
        receivedAtMillis = candidate.receivedAtMillis,
        messageId = (candidate.messageId - 1L).coerceAtLeast(-1L)
    )

    fun isAllowedSender(sender: String?, filters: List<String>): Boolean {
        val normalizedSender = sender?.trim().orEmpty()
        if (normalizedSender.isEmpty()) return false

        return filters
            .asSequence()
            .map(String::trim)
            .filter(String::isNotEmpty)
            .any { filter ->
                normalizedSender.contains(filter, ignoreCase = true) ||
                    filter.contains(normalizedSender, ignoreCase = true)
            }
    }

    fun haveEquivalentFilters(current: List<String>, candidate: List<String>): Boolean =
        normalizedFilterSet(current) == normalizedFilterSet(candidate)

    fun formatReceivedAt(receivedAtMillis: Long): String =
        Instant.ofEpochMilli(receivedAtMillis).toString()

    private fun normalizedFilterSet(filters: List<String>): Set<String> =
        filters
            .asSequence()
            .map(String::trim)
            .filter(String::isNotEmpty)
            .map { filter -> filter.lowercase(Locale.ROOT) }
            .toSet()
}
