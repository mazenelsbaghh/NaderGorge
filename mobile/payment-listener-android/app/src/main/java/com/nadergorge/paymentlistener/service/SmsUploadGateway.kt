package com.nadergorge.paymentlistener.service

import android.content.Context
import com.nadergorge.paymentlistener.data.api.ApiClient
import com.nadergorge.paymentlistener.data.api.ApiResponse
import com.nadergorge.paymentlistener.data.api.SmsUploadResponse
import com.nadergorge.paymentlistener.data.api.SmsUploadRequest as ApiSmsUploadRequest
import kotlinx.coroutines.CancellationException
import retrofit2.Response

sealed interface SmsUploadOutcome {
    data object Success : SmsUploadOutcome
    data object RetryableFailure : SmsUploadOutcome
    data object ConfigurationFailure : SmsUploadOutcome
}

internal data class WalletSmsUploadRequest(
    val pairingToken: String,
    val sender: String,
    val body: String,
    val receivedAt: String
)

internal object PairingTokenFailurePolicy {
    fun isInvalid(message: String?): Boolean =
        message?.contains("pairing token invalid", ignoreCase = true) == true

    fun isPermanentClientFailure(statusCode: Int): Boolean = when (statusCode) {
        408, 425, 429 -> false
        else -> statusCode in 400..499
    }
}

object SmsUploadGateway {
    internal suspend fun upload(
        context: Context,
        request: WalletSmsUploadRequest
    ): SmsUploadOutcome {
        val token = request.pairingToken.trim()
        if (token.isEmpty()) return SmsUploadOutcome.ConfigurationFailure

        val apiService = ApiClient.getApiService(context)
            ?: return SmsUploadOutcome.ConfigurationFailure

        return try {
            val response = apiService.uploadSms(
                token,
                ApiSmsUploadRequest(request.sender, request.body, request.receivedAt)
            )
            classify(response)
        } catch (error: CancellationException) {
            throw error
        } catch (_: Exception) {
            SmsUploadOutcome.RetryableFailure
        }
    }

    internal fun classify(
        response: Response<ApiResponse<SmsUploadResponse>>
    ): SmsUploadOutcome {
        val responseBody = response.body()
        val errorMessage = if (response.isSuccessful) null else response.errorBody()?.string()

        return when {
            response.isSuccessful && responseBody?.success == true -> SmsUploadOutcome.Success
            PairingTokenFailurePolicy.isInvalid(responseBody?.message) ||
                PairingTokenFailurePolicy.isInvalid(errorMessage) ||
                PairingTokenFailurePolicy.isPermanentClientFailure(response.code()) ->
                SmsUploadOutcome.ConfigurationFailure
            else -> SmsUploadOutcome.RetryableFailure
        }
    }
}
