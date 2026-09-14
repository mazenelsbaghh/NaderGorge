package com.nadergorge.paymentlistener.service

import android.content.Context
import android.util.Log
import androidx.work.Data
import androidx.work.CoroutineWorker
import androidx.work.WorkerParameters
import com.nadergorge.paymentlistener.data.preference.PreferenceManager
import com.nadergorge.paymentlistener.data.preference.WalletPairing
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

internal data class SmsSyncQueuePayload(
    val sender: String,
    val body: String,
    val receivedAt: String,
    val walletBindingKey: String,
    val subscriptionId: Int?,
    val simSlotIndex: Int?,
    val inboxCursor: SmsInboxCursor? = null
)

internal fun resolvePinnedSmsUploadRequest(
    payload: SmsSyncQueuePayload,
    resolvePairing: (String) -> WalletPairing?
): WalletSmsUploadRequest? {
    val pairing = resolvePairing(payload.walletBindingKey) ?: return null
    val routing = WalletRoutingPolicy.route(
        pairings = listOf(pairing),
        subscriptionId = payload.subscriptionId,
        simSlotIndex = payload.simSlotIndex
    )
    if (routing !is WalletRoutingResult.Routed ||
        !WalletRoutingPolicy.isSenderAllowed(pairing, payload.sender)
    ) {
        return null
    }
    return WalletSmsUploadRequest(
        pairingToken = pairing.token,
        sender = payload.sender,
        body = payload.body,
        receivedAt = payload.receivedAt
    )
}

internal fun shouldRetryQueuedSmsUpload(outcome: SmsUploadOutcome): Boolean =
    outcome != SmsUploadOutcome.Success

class SmsSyncWorker(
    context: Context,
    params: WorkerParameters
) : CoroutineWorker(context, params) {

    companion object {
        private const val TAG = "SmsSyncWorker"

        internal const val INPUT_SENDER = "sender"
        internal const val INPUT_BODY = "body"
        internal const val INPUT_RECEIVED_AT = "received_at"
        internal const val INPUT_WALLET_BINDING_KEY = "wallet_binding_key"
        internal const val INPUT_SUBSCRIPTION_ID = "subscription_id"
        internal const val INPUT_SIM_SLOT_INDEX = "sim_slot_index"
        internal const val INPUT_INBOX_RECEIVED_AT_MILLIS = "inbox_received_at_millis"
        internal const val INPUT_INBOX_MESSAGE_ID = "inbox_message_id"

        internal const val UNKNOWN_SUBSCRIPTION_ID = -1
        internal const val UNKNOWN_SIM_SLOT_INDEX = -1

        internal fun inputData(payload: SmsSyncQueuePayload): Data = Data.Builder()
            .putString(INPUT_SENDER, payload.sender)
            .putString(INPUT_BODY, payload.body)
            .putString(INPUT_RECEIVED_AT, payload.receivedAt)
            .putString(INPUT_WALLET_BINDING_KEY, payload.walletBindingKey)
            .putInt(
                INPUT_SUBSCRIPTION_ID,
                payload.subscriptionId ?: UNKNOWN_SUBSCRIPTION_ID
            )
            .putInt(INPUT_SIM_SLOT_INDEX, payload.simSlotIndex ?: UNKNOWN_SIM_SLOT_INDEX)
            .putLong(
                INPUT_INBOX_RECEIVED_AT_MILLIS,
                payload.inboxCursor?.receivedAtMillis ?: -1L
            )
            .putLong(INPUT_INBOX_MESSAGE_ID, payload.inboxCursor?.messageId ?: -1L)
            .build()

        internal fun parseInputData(data: Data): SmsSyncQueuePayload? {
            if (!data.keyValueMap.containsKey(INPUT_SUBSCRIPTION_ID) ||
                !data.keyValueMap.containsKey(INPUT_SIM_SLOT_INDEX) ||
                !data.keyValueMap.containsKey(INPUT_INBOX_RECEIVED_AT_MILLIS) ||
                !data.keyValueMap.containsKey(INPUT_INBOX_MESSAGE_ID)
            ) {
                return null
            }

            val inboxReceivedAt = data.getLong(INPUT_INBOX_RECEIVED_AT_MILLIS, -1L)
            val inboxMessageId = data.getLong(INPUT_INBOX_MESSAGE_ID, -1L)
            val inboxCursor = when {
                inboxReceivedAt >= 0L && inboxMessageId >= 0L ->
                    SmsInboxCursor(inboxReceivedAt, inboxMessageId)
                inboxReceivedAt == -1L && inboxMessageId == -1L -> null
                else -> return null
            }

            return SmsSyncQueuePayload(
                sender = data.getString(INPUT_SENDER) ?: return null,
                body = data.getString(INPUT_BODY) ?: return null,
                receivedAt = data.getString(INPUT_RECEIVED_AT) ?: return null,
                walletBindingKey = data.getString(INPUT_WALLET_BINDING_KEY)
                    ?.takeIf(String::isNotBlank)
                    ?: return null,
                subscriptionId = data.getInt(
                    INPUT_SUBSCRIPTION_ID,
                    UNKNOWN_SUBSCRIPTION_ID
                ).takeIf { it >= 0 },
                simSlotIndex = data.getInt(
                    INPUT_SIM_SLOT_INDEX,
                    UNKNOWN_SIM_SLOT_INDEX
                ).takeIf { it >= 0 },
                inboxCursor = inboxCursor
            )
        }

    }

    override suspend fun doWork(): Result = withContext(Dispatchers.IO) {
        val payload = parseInputData(inputData) ?: return@withContext Result.failure()
        val preferences = PreferenceManager(applicationContext)
        val request = resolvePinnedSmsUploadRequest(payload) { bindingKey ->
            preferences.getWalletPairingByQueueBindingKey(bindingKey)
        } ?: return@withContext Result.failure()

        val uploadOutcome = SmsUploadGateway.upload(
            applicationContext,
            request
        )
        if (shouldRetryQueuedSmsUpload(uploadOutcome)) {
            Log.w(TAG, "Wallet SMS upload is unavailable; retrying this message independently.")
            return@withContext Result.retry()
        }

        Log.i(TAG, "Wallet SMS uploaded successfully.")
        if (payload.inboxCursor == null || preferences.completePendingSmsUpload(
                payload.inboxCursor,
                payload.walletBindingKey
            )
        ) {
            Result.success()
        } else {
            Result.retry()
        }
    }
}
