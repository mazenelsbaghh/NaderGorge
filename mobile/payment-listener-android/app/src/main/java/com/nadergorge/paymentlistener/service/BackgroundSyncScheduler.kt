package com.nadergorge.paymentlistener.service

import android.content.Context
import android.content.Intent
import android.util.Log
import androidx.core.content.ContextCompat
import androidx.work.Constraints
import androidx.work.ExistingPeriodicWorkPolicy
import androidx.work.NetworkType
import androidx.work.PeriodicWorkRequestBuilder
import androidx.work.WorkManager
import java.util.concurrent.TimeUnit

object BackgroundSyncScheduler {
    private const val STATUS_SYNC_WORK_NAME = "wallet_status_background_sync"
    private const val TAG = "BackgroundSyncScheduler"

    fun schedule(context: Context) {
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
    }

    fun cancel(context: Context) {
        WorkManager.getInstance(context).cancelUniqueWork(STATUS_SYNC_WORK_NAME)
        stopRealtimeService(context)
    }

    fun startRealtimeService(context: Context) {
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
}
