package com.nadergorge.paymentlistener.service

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.provider.Telephony
import android.util.Log
import android.widget.Toast
import androidx.work.Data
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.WorkManager
import com.nadergorge.paymentlistener.data.preference.PreferenceManager
import java.text.SimpleDateFormat
import java.util.*

class SmsReceiver : BroadcastReceiver() {

    companion object {
        private const val TAG = "SmsReceiver"
    }

    override fun onReceive(context: Context, intent: Intent) {
        if (intent.action != Telephony.Sms.Intents.SMS_RECEIVED_ACTION) return

        val messages = Telephony.Sms.Intents.getMessagesFromIntent(intent)
        if (messages.isNullOrEmpty()) return

        val prefManager = PreferenceManager(context)
        val serverUrl = prefManager.getServerUrl()
        val token = prefManager.getPairingToken()

        // If not configured, we cannot forward anything
        if (serverUrl.isNullOrBlank() || token.isNullOrBlank()) {
            Log.w(TAG, "Device not paired. SMS received but ignored.")
            return
        }

        // Aggregate message bodies (in case of long multi-part SMS)
        val sender = messages[0].originatingAddress ?: ""
        val bodyBuilder = StringBuilder()
        for (msg in messages) {
            bodyBuilder.append(msg.messageBody)
        }
        val body = bodyBuilder.toString()
        val timestamp = messages[0].timestampMillis

        Log.d(TAG, "Received SMS from: $sender")

        // Get filters (clean and case-insensitive check)
        val filters = prefManager.getSmsFilters()
        val isSenderAllowed = filters.any { filter ->
            sender.contains(filter, ignoreCase = true) || filter.contains(sender, ignoreCase = true)
        }

        if (isSenderAllowed) {
            Log.i(TAG, "Allowed SMS detected. Scheduling synchronization...")
            
            // Format timestamp to ISO 8601 string
            val sdf = SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss'Z'", Locale.US).apply {
                timeZone = TimeZone.getTimeZone("UTC")
            }
            val isoDate = sdf.format(Date(timestamp))

            // Enqueue work to upload SMS
            val workInput = Data.Builder()
                .putString("sender", sender)
                .putString("body", body)
                .putString("received_at", isoDate)
                .build()

            val workRequest = OneTimeWorkRequestBuilder<SmsSyncWorker>()
                .setInputData(workInput)
                .build()

            WorkManager.getInstance(context).enqueue(workRequest)

            Toast.makeText(context, "تم التقاط رسالة تحويل من $sender وجاري إرسالها...", Toast.LENGTH_SHORT).show()
        } else {
            Log.d(TAG, "SMS sender '$sender' not in filter list. Ignored.")
        }
    }
}
