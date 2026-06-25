package com.nadergorge.paymentlistener.service

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.Context
import android.content.Intent
import android.os.Build
import android.os.IBinder
import android.util.Log
import androidx.core.app.NotificationCompat
import com.nadergorge.paymentlistener.MainActivity
import com.nadergorge.paymentlistener.data.api.ApiClient
import com.nadergorge.paymentlistener.data.api.SyncStatusRequest
import com.nadergorge.paymentlistener.data.preference.PreferenceManager
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch

class RealtimeSyncService : Service() {
    private val serviceScope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private var syncJob: Job? = null

    companion object {
        private const val TAG = "RealtimeSyncService"
        private const val CHANNEL_ID = "payment_listener_realtime_sync"
        private const val NOTIFICATION_ID = 2101
        private const val SYNC_INTERVAL_MS = 30_000L
    }

    override fun onCreate() {
        super.onCreate()
        createNotificationChannel()
        startForeground(NOTIFICATION_ID, buildNotification())
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        if (syncJob?.isActive != true) {
            syncJob = serviceScope.launch {
                while (isActive) {
                    syncOnce()
                    delay(SYNC_INTERVAL_MS)
                }
            }
        }

        return START_STICKY
    }

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onDestroy() {
        syncJob?.cancel()
        serviceScope.coroutineContext[Job]?.cancel()
        super.onDestroy()
    }

    private suspend fun syncOnce() {
        val prefManager = PreferenceManager(applicationContext)
        val token = prefManager.getPairingToken()
        val apiService = ApiClient.getApiService(applicationContext)

        if (token.isNullOrBlank() || apiService == null) {
            Log.w(TAG, "Stopping realtime sync because device is not paired.")
            stopSelf()
            return
        }

        try {
            val response = apiService.syncStatus(
                token,
                SyncStatusRequest(prefManager.getLastBalance().toDouble())
            )

            if (response.isSuccessful && response.body()?.success == true) {
                val data = response.body()?.data
                if (data != null) {
                    prefManager.saveSmsFilters(data.smsSenderFilters)
                    prefManager.saveLastBalance(data.currentBalance.toFloat())
                    prefManager.saveDevicePhone(data.phoneNumber)
                    prefManager.saveDeviceLabel(data.label)
                }
                Log.i(TAG, "Realtime status sync completed.")
            } else {
                Log.e(TAG, "Realtime status sync failed: ${response.code()} ${response.message()}")
            }
        } catch (e: Exception) {
            Log.e(TAG, "Realtime status sync exception", e)
        }
    }

    private fun createNotificationChannel() {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) return

        val channel = NotificationChannel(
            CHANNEL_ID,
            "Payment listener connection",
            NotificationManager.IMPORTANCE_LOW
        ).apply {
            description = "Keeps the wallet listener connected to Massar."
            setShowBadge(false)
        }

        val manager = getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager
        manager.createNotificationChannel(channel)
    }

    private fun buildNotification(): Notification {
        val pendingIntent = PendingIntent.getActivity(
            this,
            0,
            Intent(this, MainActivity::class.java),
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT
        )

        return NotificationCompat.Builder(this, CHANNEL_ID)
            .setSmallIcon(android.R.drawable.stat_notify_sync)
            .setContentTitle("مستمع المدفوعات يعمل")
            .setContentText("يتم الاتصال بالسيرفر ومزامنة رسائل المحافظ تلقائياً.")
            .setContentIntent(pendingIntent)
            .setOngoing(true)
            .setPriority(NotificationCompat.PRIORITY_LOW)
            .build()
    }
}
