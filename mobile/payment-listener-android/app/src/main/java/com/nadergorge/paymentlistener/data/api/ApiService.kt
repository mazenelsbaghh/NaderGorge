package com.nadergorge.paymentlistener.data.api

import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.Header
import retrofit2.http.POST

interface ApiService {

    @POST("api/android/sync-status")
    suspend fun syncStatus(
        @Header("X-Pairing-Token") token: String,
        @Body request: SyncStatusRequest
    ): Response<ApiResponse<SyncStatusResponse>>

    @POST("api/android/sms")
    suspend fun uploadSms(
        @Header("X-Pairing-Token") token: String,
        @Body request: SmsUploadRequest
    ): Response<ApiResponse<SmsUploadResponse>>
}

// Request & Response DTOs
data class SyncStatusRequest(
    val currentBalance: Double?
)

data class ApiResponse<T>(
    val success: Boolean,
    val data: T?,
    val message: String?
)

data class SyncStatusResponse(
    val phoneNumber: String,
    val label: String,
    val dailyLimit: Double,
    val monthlyLimit: Double,
    val dailyReceived: Double,
    val monthlyReceived: Double,
    val currentBalance: Double,
    val isActive: Boolean,
    val smsSenderFilters: List<String>,
    val otherDevices: List<OtherDeviceDto>
)

data class OtherDeviceDto(
    val phoneNumber: String,
    val label: String,
    val currentBalance: Double,
    val deviceStatus: String,
    val lastSeenAt: String?
)

data class SmsUploadRequest(
    val sender: String,
    val body: String,
    val receivedAt: String // ISO 8601 string, e.g. "2026-06-25T19:00:00Z"
)

data class SmsUploadResponse(
    val isMatched: Boolean,
    val message: String?
)
