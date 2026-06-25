package com.nadergorge.paymentlistener.service

import android.content.Context
import android.util.Log
import androidx.work.CoroutineWorker
import androidx.work.WorkerParameters
import com.nadergorge.paymentlistener.data.api.ApiClient
import com.nadergorge.paymentlistener.data.api.SmsUploadRequest
import com.nadergorge.paymentlistener.data.preference.PreferenceManager
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

        val prefManager = PreferenceManager(applicationContext)
        val token = prefManager.getPairingToken() ?: return@withContext Result.failure()
        val apiService = ApiClient.getApiService(applicationContext) ?: return@withContext Result.failure()

        Log.i(TAG, "Uploading SMS from $sender...")

        try {
            val request = SmsUploadRequest(sender, body, receivedAt)
            val response = apiService.uploadSms(token, request)

            if (response.isSuccessful) {
                val apiResponse = response.body()
                if (apiResponse?.success == true) {
                    Log.i(TAG, "SMS uploaded successfully: ${apiResponse.message}")
                    return@withContext Result.success()
                } else {
                    val msg = apiResponse?.message ?: "Unknown error"
                    Log.e(TAG, "Server rejected SMS upload: $msg")
                    
                    // If token is invalid, don't retry
                    if (msg.contains("pairing token invalid", ignoreCase = true)) {
                        return@withContext Result.failure()
                    }
                    return@withContext Result.retry()
                }
            } else {
                Log.e(TAG, "HTTP error uploading SMS: ${response.code()} ${response.message()}")
                return@withContext Result.retry()
            }
        } catch (e: Exception) {
            Log.e(TAG, "Exception uploading SMS", e)
            return@withContext Result.retry()
        }
    }
}
