package com.nadergorge.paymentlistener.service

import android.content.Context
import android.content.Intent
import android.util.Log
import androidx.core.content.ContextCompat
import androidx.work.Constraints
import androidx.work.ExistingPeriodicWorkPolicy
import androidx.work.ExistingWorkPolicy
import androidx.work.NetworkType
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.PeriodicWorkRequestBuilder
import androidx.work.WorkManager
import com.nadergorge.paymentlistener.data.preference.PreferenceManager
import java.util.concurrent.TimeUnit

object BackgroundSyncScheduler {
    private const val STATUS_SYNC_WORK_NAME = "wallet_status_background_sync"
    private const val SMS_RECONCILIATION_WORK_NAME = "wallet_sms_inbox_reconciliation"
    private const val SMS_RECONCILIATION_NOW_WORK_NAME = "wallet_sms_inbox_reconciliation_now"
    private const val TAG = "BackgroundSyncScheduler"

    fun schedule(context: Context) {
        val preferences = PreferenceManager(context)
        if (preferences.getServerUrl().isNullOrBlank() || !preferences.hasWalletPairings()) {
            Log.w(TAG, "Background synchronization was not scheduled because no wallet is paired.")
            return
        }
        preferences.ensureSmsReconciliationCursor(System.currentTimeMillis())

        val constraints = Constraints.Builder()
            .setRequiredNetworkType(NetworkType.CONNECTED)
            .build()

        val request = PeriodicWorkRequestBuilder<StatusSyncWorker>(15, TimeUnit.MINUTES)
            .setConstraints(constraints)
            .build()

        WorkManager.getInstance(context).enqueueUniquePeriodicWork(
            STATUS_SYNC_WORK_NAME,
            ExistingPeriodicWorkPolicy.UPDATE,
            request
        )

        val reconciliationRequest = PeriodicWorkRequestBuilder<SmsInboxReconciliationWorker>(15, TimeUnit.MINUTES)
            .setConstraints(constraints)
            .build()
        WorkManager.getInstance(context).enqueueUniquePeriodicWork(
            SMS_RECONCILIATION_WORK_NAME,
            ExistingPeriodicWorkPolicy.UPDATE,
            reconciliationRequest
        )

        val immediateReconciliation = OneTimeWorkRequestBuilder<SmsInboxReconciliationWorker>()
            .setConstraints(constraints)
            .build()
        WorkManager.getInstance(context).enqueueUniqueWork(
            SMS_RECONCILIATION_NOW_WORK_NAME,
            ExistingWorkPolicy.KEEP,
            immediateReconciliation
        )
    }

    fun startRealtimeService(context: Context) {
        val preferences = PreferenceManager(context)
        if (preferences.getServerUrl().isNullOrBlank() || !preferences.hasWalletPairings()) {
            Log.w(TAG, "Realtime synchronization was not started because no wallet is paired.")
            return
        }

        try {
            ContextCompat.startForegroundService(
                context,
                Intent(context, RealtimeSyncService::class.java)
            )
        } catch (e: Exception) {
            Log.e(TAG, "Unable to start realtime sync service.", e)
        }
    }

    fun stopRealtimeService(context: Context) {
        context.stopService(Intent(context, RealtimeSyncService::class.java))
    }

    fun cancel(context: Context) {
        WorkManager.getInstance(context).cancelUniqueWork(STATUS_SYNC_WORK_NAME)
        WorkManager.getInstance(context).cancelUniqueWork(SMS_RECONCILIATION_WORK_NAME)
        WorkManager.getInstance(context).cancelUniqueWork(SMS_RECONCILIATION_NOW_WORK_NAME)
        stopRealtimeService(context)
    }
}
