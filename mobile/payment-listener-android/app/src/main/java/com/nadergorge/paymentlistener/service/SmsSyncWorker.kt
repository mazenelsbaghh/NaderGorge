package com.nadergorge.paymentlistener.service

import android.content.Context
import android.util.Log
import androidx.work.CoroutineWorker
import androidx.work.WorkerParameters
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

class SmsSyncWorker(
    context: Context,
    params: WorkerParameters
) : CoroutineWorker(context, params) {

    companion object {
        private const val TAG = "SmsSyncWorker"
    }

    override suspend fun doWork(): Result = withContext(Dispatchers.IO) {
        val sender = inputData.getString("sender") ?: return@withContext Result.failure()
        val body = inputData.getString("body") ?: return@withContext Result.failure()
        val receivedAt = inputData.getString("received_at") ?: return@withContext Result.failure()

        return@withContext when (SmsUploadGateway.upload(
            applicationContext,
            sender,
            body,
            receivedAt
        )) {
            SmsUploadOutcome.Success -> {
                Log.i(TAG, "Wallet SMS uploaded successfully.")
                Result.success()
            }
            SmsUploadOutcome.RetryableFailure -> {
                Log.w(TAG, "Wallet SMS upload will be retried.")
                Result.retry()
            }
            SmsUploadOutcome.ConfigurationFailure -> {
                Log.e(TAG, "Wallet SMS upload configuration is unavailable.")
                Result.failure()
            }
        }
    }
}
