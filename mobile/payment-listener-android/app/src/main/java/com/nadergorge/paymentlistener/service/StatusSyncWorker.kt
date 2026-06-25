package com.nadergorge.paymentlistener.service

import android.content.Context
import android.util.Log
import androidx.work.CoroutineWorker
import androidx.work.WorkerParameters
import com.nadergorge.paymentlistener.data.api.ApiClient
import com.nadergorge.paymentlistener.data.api.SyncStatusRequest
import com.nadergorge.paymentlistener.data.preference.PreferenceManager
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
        val prefManager = PreferenceManager(applicationContext)
        val token = prefManager.getPairingToken()
        val apiService = ApiClient.getApiService(applicationContext)

        if (token.isNullOrBlank() || apiService == null) {
            Log.w(TAG, "Skipping status sync because device is not paired.")
            return@withContext Result.failure()
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
                Log.i(TAG, "Background status sync completed.")
                Result.success()
            } else {
                Log.e(TAG, "Background status sync failed: ${response.code()} ${response.message()}")
                Result.retry()
            }
        } catch (e: Exception) {
            Log.e(TAG, "Background status sync exception", e)
            Result.retry()
        }
    }
}
