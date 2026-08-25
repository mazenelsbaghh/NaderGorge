package com.nadergorge.paymentlistener.service

import android.Manifest
import android.content.Context
import android.content.pm.PackageManager
import android.database.Cursor
import android.provider.Telephony
import android.util.Log
import androidx.core.content.ContextCompat
import androidx.work.CoroutineWorker
import androidx.work.WorkerParameters
import com.nadergorge.paymentlistener.data.preference.PreferenceManager
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.ensureActive
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withContext

private val smsInboxReconciliationMutex = Mutex()

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
        if (preferences.getPairingToken().isNullOrBlank() || preferences.getServerUrl().isNullOrBlank()) {
            return@withContext Result.failure()
        }

        val state = preferences.getSmsReconciliationState(System.currentTimeMillis())
        try {
            val inbox = queryInbox(state.cursor) ?: return@withContext Result.retry()
            inbox.use { reconcileMessages(it, preferences, state) }
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
            Telephony.Sms.DATE
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
        preferences: PreferenceManager,
        state: SmsInboxReconciliationState
    ): Result {
        val idIndex = inbox.getColumnIndexOrThrow(Telephony.Sms._ID)
        val senderIndex = inbox.getColumnIndexOrThrow(Telephony.Sms.ADDRESS)
        val bodyIndex = inbox.getColumnIndexOrThrow(Telephony.Sms.BODY)
        val dateIndex = inbox.getColumnIndexOrThrow(Telephony.Sms.DATE)
        var inspected = 0

        while (inspected < MAX_MESSAGES_PER_RUN && inbox.moveToNext()) {
            currentCoroutineContext().ensureActive()
            if (!preferences.isSmsFilterRevisionCurrent(state.filterRevision)) {
                return Result.retry()
            }

            val messageId = inbox.getLong(idIndex)
            val receivedAtMillis = inbox.getLong(dateIndex)
            val sender = inbox.getString(senderIndex)?.trim().orEmpty()
            val body = inbox.getString(bodyIndex).orEmpty()
            inspected++

            if (!SmsInboxReconciliationPolicy.isAfter(receivedAtMillis, messageId, state.cursor)) {
                continue
            }

            if (SmsInboxReconciliationPolicy.isAllowedSender(sender, state.senderFilters)) {
                when (uploadMessage(sender, body, receivedAtMillis)) {
                    SmsUploadOutcome.Success -> Unit
                    SmsUploadOutcome.RetryableFailure -> return Result.retry()
                    SmsUploadOutcome.ConfigurationFailure -> return Result.failure()
                }
            }

            val cursorAdvanced = preferences.advanceSmsReconciliationCursor(
                expectedFilterRevision = state.filterRevision,
                candidate = SmsInboxCursor(receivedAtMillis, messageId)
            )
            if (!cursorAdvanced) return Result.retry()
        }

        return if (inspected == MAX_MESSAGES_PER_RUN) Result.retry() else Result.success()
    }

    private suspend fun uploadMessage(
        sender: String,
        body: String,
        receivedAtMillis: Long
    ): SmsUploadOutcome = SmsUploadGateway.upload(
        applicationContext,
        sender = sender,
        body = body,
        receivedAt = SmsInboxReconciliationPolicy.formatReceivedAt(receivedAtMillis)
    )
}
