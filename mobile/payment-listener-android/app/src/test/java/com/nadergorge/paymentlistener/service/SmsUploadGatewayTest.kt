package com.nadergorge.paymentlistener.service

import com.nadergorge.paymentlistener.data.api.ApiResponse
import com.nadergorge.paymentlistener.data.api.SmsUploadResponse
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertSame
import org.junit.Test
import retrofit2.Response

class SmsUploadGatewayTest {
    @Test
    fun successfulApiResponseIsAccepted() {
        val response = Response.success(
            ApiResponse(
                success = true,
                data = SmsUploadResponse(isMatched = true, message = "matched"),
                message = "ok"
            )
        )

        assertSame(SmsUploadOutcome.Success, SmsUploadGateway.classify(response))
    }

    @Test
    fun invalidTokenErrorIsConfigurationFailure() {
        val response = Response.error<ApiResponse<SmsUploadResponse>>(
            503,
            """{"success":false,"message":"pairing token invalid"}"""
                .toResponseBody("application/json".toMediaType())
        )

        assertSame(SmsUploadOutcome.ConfigurationFailure, SmsUploadGateway.classify(response))
    }

    @Test
    fun transientServerErrorRemainsRetryable() {
        val response = Response.error<ApiResponse<SmsUploadResponse>>(
            503,
            """{"success":false,"message":"temporarily unavailable"}"""
                .toResponseBody("application/json".toMediaType())
        )

        assertSame(SmsUploadOutcome.RetryableFailure, SmsUploadGateway.classify(response))
    }

    @Test
    fun inactiveWalletClientErrorIsConfigurationFailureForRetryPolicy() {
        val response = Response.error<ApiResponse<SmsUploadResponse>>(
            400,
            """{"success":false,"message":"هذه المحفظة غير نشطة حالياً"}"""
                .toResponseBody("application/json".toMediaType())
        )

        assertSame(SmsUploadOutcome.ConfigurationFailure, SmsUploadGateway.classify(response))
    }

    @Test
    fun rateLimitClientErrorRemainsRetryable() {
        val response = Response.error<ApiResponse<SmsUploadResponse>>(
            429,
            """{"success":false,"message":"too many requests"}"""
                .toResponseBody("application/json".toMediaType())
        )

        assertSame(SmsUploadOutcome.RetryableFailure, SmsUploadGateway.classify(response))
    }
}
