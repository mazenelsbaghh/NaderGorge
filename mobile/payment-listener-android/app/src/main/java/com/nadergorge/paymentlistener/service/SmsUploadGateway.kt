package com.nadergorge.paymentlistener.service

import android.content.Context
import com.nadergorge.paymentlistener.data.api.ApiClient
import com.nadergorge.paymentlistener.data.api.SmsUploadRequest
import com.nadergorge.paymentlistener.data.preference.PreferenceManager
import kotlinx.coroutines.CancellationException

sealed interface SmsUploadOutcome {
    data object Success : SmsUploadOutcome
    data object RetryableFailure : SmsUploadOutcome
    data object ConfigurationFailure : SmsUploadOutcome
}

object SmsUploadGateway {
    suspend fun upload(
        context: Context,
        sender: String,
        body: String,
        receivedAt: String
    ): SmsUploadOutcome {
        val preferences = PreferenceManager(context)
        val token = preferences.getPairingToken()
            ?: return SmsUploadOutcome.ConfigurationFailure
        val apiService = ApiClient.getApiService(context)
            ?: return SmsUploadOutcome.ConfigurationFailure

        return try {
            val response = apiService.uploadSms(token, SmsUploadRequest(sender, body, receivedAt))
            if (!response.isSuccessful) {
                SmsUploadOutcome.RetryableFailure
            } else {
                val apiResponse = response.body()
                when {
                    apiResponse?.success == true -> SmsUploadOutcome.Success
                    apiResponse?.message?.contains("pairing token invalid", ignoreCase = true) == true ->
                        SmsUploadOutcome.ConfigurationFailure
                    else -> SmsUploadOutcome.RetryableFailure
                }
            }
        } catch (error: CancellationException) {
            throw error
        } catch (_: Exception) {
            SmsUploadOutcome.RetryableFailure
        }
    }
}
