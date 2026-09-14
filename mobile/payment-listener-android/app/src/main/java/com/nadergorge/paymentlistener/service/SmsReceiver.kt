package com.nadergorge.paymentlistener.service

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.provider.Telephony
import android.telephony.SubscriptionManager
import android.util.Log
import android.widget.Toast
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.WorkManager
import com.nadergorge.paymentlistener.data.preference.PreferenceManager
import com.nadergorge.paymentlistener.data.preference.WalletPairing
import com.nadergorge.paymentlistener.data.sim.SimSubscriptionReader

internal data class IncomingSmsSimIdentity(
    val subscriptionId: Int,
    val simSlotIndex: Int?
)

internal fun resolveIncomingSmsSimIdentity(
    broadcastSubscriptionId: Int?,
    broadcastSimSlotIndex: Int?,
    slotsBySubscriptionId: Map<Int, Int>
): IncomingSmsSimIdentity? {
    if (broadcastSubscriptionId != null) {
        val activeSlotIndex = slotsBySubscriptionId[broadcastSubscriptionId]
        val activeSubscriptionInBroadcastSlot = broadcastSimSlotIndex?.let { slotIndex ->
            slotsBySubscriptionId.entries.singleOrNull { entry -> entry.value == slotIndex }?.key
        }
        if (broadcastSimSlotIndex != null &&
            ((activeSlotIndex != null && activeSlotIndex != broadcastSimSlotIndex) ||
                (activeSubscriptionInBroadcastSlot != null &&
                    activeSubscriptionInBroadcastSlot != broadcastSubscriptionId))
        ) {
            return null
        }
        return IncomingSmsSimIdentity(
            subscriptionId = broadcastSubscriptionId,
            simSlotIndex = broadcastSimSlotIndex ?: activeSlotIndex
        )
    }

    if (broadcastSimSlotIndex != null) {
        val matchingSubscriptionId = slotsBySubscriptionId.entries
            .singleOrNull { entry -> entry.value == broadcastSimSlotIndex }
            ?.key
            ?: return null
        return IncomingSmsSimIdentity(matchingSubscriptionId, broadcastSimSlotIndex)
    }

    return slotsBySubscriptionId.entries.singleOrNull()?.let { entry ->
        IncomingSmsSimIdentity(entry.key, entry.value)
    }
}

class SmsReceiver : BroadcastReceiver() {

    companion object {
        private const val TAG = "SmsReceiver"
        private const val LEGACY_SUBSCRIPTION_EXTRA = "subscription"
        private const val LEGACY_SLOT_EXTRA = "slot"

    }

    override fun onReceive(context: Context, intent: Intent) {
        if (intent.action != Telephony.Sms.Intents.SMS_RECEIVED_ACTION) return

        val messages = Telephony.Sms.Intents.getMessagesFromIntent(intent)
        if (messages.isNullOrEmpty()) return

        val prefManager = PreferenceManager(context)
        val serverUrl = prefManager.getServerUrl()
        val pairings = prefManager.getWalletPairings()

        if (serverUrl.isNullOrBlank() || pairings.isEmpty()) {
            Log.w(TAG, "Device not paired. SMS received but ignored.")
            return
        }

        val sender = messages[0].originatingAddress ?: ""
        val body = messages.joinToString(separator = "") { message ->
            message.messageBody.orEmpty()
        }
        val timestamp = messages[0].timestampMillis

        val broadcastSubscriptionId = intent.firstNonNegativeIntExtra(
            SubscriptionManager.EXTRA_SUBSCRIPTION_INDEX,
            LEGACY_SUBSCRIPTION_EXTRA,
            Telephony.Sms.SUBSCRIPTION_ID
        )
        val broadcastSimSlotIndex = intent.firstNonNegativeIntExtra(
            SubscriptionManager.EXTRA_SLOT_INDEX,
            LEGACY_SLOT_EXTRA
        )
        val activeSubscriptions = try {
            SimSubscriptionReader(context).activeSubscriptions()
        } catch (_: SecurityException) {
            emptyList()
        }
        val slotsBySubscriptionId = activeSubscriptions.associate { subscription ->
            subscription.subscriptionId to subscription.slotIndex
        }
        val incomingSimIdentity = resolveIncomingSmsSimIdentity(
            broadcastSubscriptionId,
            broadcastSimSlotIndex,
            slotsBySubscriptionId
        )
        if (incomingSimIdentity == null) {
            Log.w(TAG, "SMS ignored because its receiving SIM identity could not be established.")
            showRoutingAlertForFinancialSender(context, pairings, sender)
            return
        }
        val subscriptionId = incomingSimIdentity.subscriptionId
        val simSlotIndex = incomingSimIdentity.simSlotIndex
        val pairing = when (
            val routing = WalletRoutingPolicy.route(pairings, subscriptionId, simSlotIndex)
        ) {
            is WalletRoutingResult.Routed -> routing.pairing
            WalletRoutingResult.NoWallets -> {
                Log.w(TAG, "SMS ignored because there are no paired wallets.")
                return
            }
            WalletRoutingResult.NoMatch -> {
                Log.w(TAG, "SMS ignored because its SIM does not match a paired wallet.")
                showRoutingAlertForFinancialSender(context, pairings, sender)
                return
            }
            WalletRoutingResult.Ambiguous -> {
                Log.w(TAG, "SMS ignored because its SIM cannot be resolved unambiguously.")
                showRoutingAlertForFinancialSender(context, pairings, sender)
                return
            }
        }

        Log.d(TAG, "SMS broadcast received.")

        val isSenderAllowed = WalletRoutingPolicy.isSenderAllowed(pairing, sender)

        if (isSenderAllowed) {
            Log.i(TAG, "Allowed SMS detected. Scheduling synchronization...")

            val isoDate = SmsInboxReconciliationPolicy.formatReceivedAt(timestamp)
            val walletBindingKey = prefManager.getWalletQueueBindingKey(pairing.token)
            if (walletBindingKey == null) {
                Log.e(TAG, "Allowed SMS ignored because its secure wallet binding is unavailable.")
                return
            }

            val workInput = SmsSyncWorker.inputData(
                SmsSyncQueuePayload(
                    sender = sender,
                    body = body,
                    receivedAt = isoDate,
                    walletBindingKey = walletBindingKey,
                    subscriptionId = subscriptionId,
                    simSlotIndex = simSlotIndex
                )
            )

            val workRequest = OneTimeWorkRequestBuilder<SmsSyncWorker>()
                .setInputData(workInput)
                .build()

            WorkManager.getInstance(context).enqueue(workRequest)

            Toast.makeText(context, "تم التقاط رسالة تحويل وجاري إرسالها...", Toast.LENGTH_SHORT).show()
        } else {
            Log.d(TAG, "SMS sender is not in the configured filter list. Ignored.")
        }
    }

    @Suppress("DEPRECATION")
    private fun Intent.firstNonNegativeIntExtra(vararg keys: String): Int? {
        val extras = extras ?: return null
        return keys.asSequence()
            .mapNotNull { key -> extras.get(key) as? Number }
            .map(Number::toInt)
            .firstOrNull { value -> value >= 0 }
    }

    private fun showRoutingAlertForFinancialSender(
        context: Context,
        pairings: List<WalletPairing>,
        sender: String
    ) {
        if (pairings.any { pairing -> WalletRoutingPolicy.isSenderAllowed(pairing, sender) }) {
            WalletRoutingAlertNotifier.show(context)
        }
    }
}
