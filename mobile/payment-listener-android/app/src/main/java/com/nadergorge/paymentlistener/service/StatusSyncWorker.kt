package com.nadergorge.paymentlistener.service

import android.content.Context
import android.util.Log
import androidx.work.CoroutineWorker
import androidx.work.WorkerParameters
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

class StatusSyncWorker(
    context: Context,
    params: WorkerParameters
) : CoroutineWorker(context, params) {

    companion object {
        private const val TAG = "StatusSyncWorker"
    }

    override suspend fun doWork(): Result = withContext(Dispatchers.IO) {
        val summary = WalletStatusSynchronizer.syncAll(applicationContext)
        Log.i(
            TAG,
            "Background status sync completed for ${summary.successfulCount}/${summary.attemptedCount} wallets."
        )

        when {
            summary.attemptedCount == 0 -> Result.failure()
            summary.retryableFailureCount > 0 -> Result.retry()
            summary.configurationFailureCount > 0 -> Result.failure()
            else -> Result.success()
        }
    }
}
