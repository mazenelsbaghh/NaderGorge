package com.nadergorge.paymentlistener.service

import android.Manifest
import android.content.Context
import android.content.pm.PackageManager
import android.database.Cursor
import android.provider.Telephony
import android.util.Log
import androidx.core.content.ContextCompat
import androidx.work.CoroutineWorker
import androidx.work.ExistingWorkPolicy
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.WorkManager
import androidx.work.WorkerParameters
import androidx.work.await
import com.nadergorge.paymentlistener.data.preference.PreferenceManager
import com.nadergorge.paymentlistener.data.preference.WalletPairing
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.ensureActive
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withContext

private val smsInboxReconciliationMutex = Mutex()
private const val INBOX_UPLOAD_WORK_PREFIX = "wallet_sms_inbox_upload"

internal enum class PendingSmsMutation {
    REMEMBER,
    CLEAR
}

internal fun pendingSmsMutation(
    routing: WalletRoutingResult,
    senderRelevantToAnyWallet: Boolean,
    senderAllowedForRoutedWallet: Boolean
): PendingSmsMutation = when {
    routing !is WalletRoutingResult.Routed && senderRelevantToAnyWallet ->
        PendingSmsMutation.REMEMBER
    routing is WalletRoutingResult.Routed && senderAllowedForRoutedWallet ->
        PendingSmsMutation.REMEMBER
    else -> PendingSmsMutation.CLEAR
}

internal suspend fun persistEnqueueAndAdvanceInboxSms(
    persistPending: () -> Boolean,
    enqueueUpload: suspend () -> Boolean,
    advanceCursor: () -> Boolean
): Boolean {
    if (!persistPending()) return false
    if (!enqueueUpload()) return false
    return advanceCursor()
}

internal fun inboxUploadWorkName(
    cursor: SmsInboxCursor,
    walletBindingKey: String
): String = listOf(
    INBOX_UPLOAD_WORK_PREFIX,
    cursor.receivedAtMillis,
    cursor.messageId,
    walletBindingKey
).joinToString("_")

internal fun routeInboxWallet(
    pairings: List<WalletPairing>,
    subscriptionId: Int?
): WalletRoutingResult {
    if (subscriptionId == null) return WalletRoutingResult.Ambiguous
    return WalletRoutingPolicy.route(
        pairings,
        subscriptionId,
        simSlotIndex = null
    )
}

class SmsInboxReconciliationWorker(
    context: Context,
    params: WorkerParameters
) : CoroutineWorker(context, params) {
    companion object {
        private const val TAG = "SmsInboxReconcile"
        private const val MAX_MESSAGES_PER_RUN = 200
    }

    override suspend fun doWork(): Result = smsInboxReconciliationMutex.withLock {
        reconcileInbox()
    }

    private suspend fun reconcileInbox(): Result = withContext(Dispatchers.IO) {
        if (!hasReadPermission()) return@withContext Result.success()

        val preferences = PreferenceManager(applicationContext)
        val snapshot = preferences.getSmsReconciliationSnapshot(System.currentTimeMillis())
        if (snapshot.pairings.isEmpty() || preferences.getServerUrl().isNullOrBlank()) {
            return@withContext Result.failure()
        }

        try {
            val inbox = queryInbox(snapshot.state.cursor) ?: return@withContext Result.retry()
            inbox.use { cursor ->
                reconcileMessages(
                    cursor,
                    ReconciliationContext(
                        preferences = preferences,
                        state = snapshot.state
                    )
                )
            }
        } catch (error: CancellationException) {
            throw error
        } catch (_: SecurityException) {
            Log.w(TAG, "SMS inbox permission became unavailable during reconciliation.")
            Result.success()
        } catch (error: Exception) {
            Log.w(TAG, "SMS inbox reconciliation will be retried.", error)
            Result.retry()
        }
    }

    private fun hasReadPermission(): Boolean =
        ContextCompat.checkSelfPermission(
            applicationContext,
            Manifest.permission.READ_SMS
        ) == PackageManager.PERMISSION_GRANTED

    private fun queryInbox(cursor: SmsInboxCursor): Cursor? {
        val projection = arrayOf(
            Telephony.Sms._ID,
            Telephony.Sms.ADDRESS,
            Telephony.Sms.BODY,
            Telephony.Sms.DATE,
            Telephony.Sms.SUBSCRIPTION_ID
        )
        val selection = "(${Telephony.Sms.DATE} > ?) OR (${Telephony.Sms.DATE} = ? AND ${Telephony.Sms._ID} > ?)"
        val selectionArgs = arrayOf(
            cursor.receivedAtMillis.toString(),
            cursor.receivedAtMillis.toString(),
            cursor.messageId.toString()
        )

        return applicationContext.contentResolver.query(
            Telephony.Sms.Inbox.CONTENT_URI,
            projection,
            selection,
            selectionArgs,
            "${Telephony.Sms.DATE} ASC, ${Telephony.Sms._ID} ASC"
        )
    }

    private suspend fun reconcileMessages(
        inbox: Cursor,
        context: ReconciliationContext
    ): Result {
        val idIndex = inbox.getColumnIndexOrThrow(Telephony.Sms._ID)
        val senderIndex = inbox.getColumnIndexOrThrow(Telephony.Sms.ADDRESS)
        val bodyIndex = inbox.getColumnIndexOrThrow(Telephony.Sms.BODY)
        val dateIndex = inbox.getColumnIndexOrThrow(Telephony.Sms.DATE)
        val subscriptionIndex = inbox.getColumnIndexOrThrow(Telephony.Sms.SUBSCRIPTION_ID)
        var inspected = 0

        while (inspected < MAX_MESSAGES_PER_RUN && inbox.moveToNext()) {
            currentCoroutineContext().ensureActive()
            val pairings = context.preferences.getWalletPairingsIfRevisionCurrent(
                context.state.filterRevision
            ) ?: return Result.retry()

            val messageId = inbox.getLong(idIndex)
            val receivedAtMillis = inbox.getLong(dateIndex)
            val sender = inbox.getString(senderIndex)?.trim().orEmpty()
            val body = inbox.getString(bodyIndex).orEmpty()
            val subscriptionId = if (inbox.isNull(subscriptionIndex)) {
                null
            } else {
                inbox.getInt(subscriptionIndex).takeIf { value -> value >= 0 }
            }
            inspected++

            if (!SmsInboxReconciliationPolicy.isAfter(
                    receivedAtMillis,
                    messageId,
                    context.state.cursor
                )
            ) {
                continue
            }

            val routing = routeInboxWallet(
                pairings,
                subscriptionId
            )
            val messageCursor = SmsInboxCursor(receivedAtMillis, messageId)
            val isRelevantSender = pairings.any { pairing ->
                WalletRoutingPolicy.isSenderAllowed(pairing, sender)
            }
            val senderAllowedForRoutedWallet = routing is WalletRoutingResult.Routed &&
                WalletRoutingPolicy.isSenderAllowed(routing.pairing, sender)
            val walletBindingKey = if (
                routing is WalletRoutingResult.Routed && senderAllowedForRoutedWallet
            ) {
                context.preferences.getWalletQueueBindingKey(routing.pairing.token)
                    ?: return Result.retry()
            } else {
                null
            }
            if (routing !is WalletRoutingResult.Routed) {
                if (isRelevantSender) {
                    WalletRoutingAlertNotifier.show(applicationContext)
                }
                if (routing === WalletRoutingResult.Ambiguous) {
                    Log.w(TAG, "Inbox SMS skipped because its receiving SIM is ambiguous.")
                } else if (routing === WalletRoutingResult.NoMatch) {
                    Log.w(TAG, "Inbox SMS skipped because its SIM is not paired to a wallet.")
                }
            }

            val persistPending = {
                when (pendingSmsMutation(routing, isRelevantSender, senderAllowedForRoutedWallet)) {
                    PendingSmsMutation.REMEMBER -> context.preferences.rememberPendingSms(
                        context.state.filterRevision,
                        messageCursor,
                        walletBindingKey
                    )
                    PendingSmsMutation.CLEAR -> context.preferences.clearPendingSms(
                        context.state.filterRevision,
                        messageCursor
                    )
                }
            }

            if (routing is WalletRoutingResult.Routed && senderAllowedForRoutedWallet) {
                val persistedAndQueued = persistEnqueueAndAdvanceInboxSms(
                    persistPending = persistPending,
                    enqueueUpload = {
                        enqueueInboxUpload(
                            bindingKey = checkNotNull(walletBindingKey),
                            sender = sender,
                            body = body,
                            receivedAtMillis = receivedAtMillis,
                            subscriptionId = subscriptionId,
                            messageCursor = messageCursor
                        )
                    },
                    advanceCursor = {
                        context.preferences.advanceSmsReconciliationCursor(
                            expectedFilterRevision = context.state.filterRevision,
                            candidate = messageCursor
                        )
                    }
                )
                if (!persistedAndQueued) return Result.retry()
                continue
            }

            if (!persistPending()) return Result.retry()
            val cursorAdvanced = context.preferences.advanceSmsReconciliationCursor(
                expectedFilterRevision = context.state.filterRevision,
                candidate = messageCursor
            )
            if (!cursorAdvanced) return Result.retry()
        }

        return if (inspected == MAX_MESSAGES_PER_RUN) Result.retry() else Result.success()
    }

    private suspend fun enqueueInboxUpload(
        bindingKey: String,
        sender: String,
        body: String,
        receivedAtMillis: Long,
        subscriptionId: Int?,
        messageCursor: SmsInboxCursor
    ): Boolean {
        val workRequest = OneTimeWorkRequestBuilder<SmsSyncWorker>()
            .setInputData(
                SmsSyncWorker.inputData(
                    SmsSyncQueuePayload(
                        sender = sender,
                        body = body,
                        receivedAt = SmsInboxReconciliationPolicy.formatReceivedAt(receivedAtMillis),
                        walletBindingKey = bindingKey,
                        subscriptionId = subscriptionId,
                        simSlotIndex = null,
                        inboxCursor = messageCursor
                    )
                )
            )
            .build()
        WorkManager.getInstance(applicationContext).enqueueUniqueWork(
            inboxUploadWorkName(messageCursor, bindingKey),
            ExistingWorkPolicy.KEEP,
            workRequest
        ).await()
        return true
    }

    private data class ReconciliationContext(
        val preferences: PreferenceManager,
        val state: SmsInboxReconciliationState
    )
}
