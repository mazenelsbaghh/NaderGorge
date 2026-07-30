package com.nadergorge.parent.data.api

import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.Header
import retrofit2.http.POST

interface ParentApiService {
    @POST("api/parent/verify-code")
    suspend fun verifyCode(
        @Body request: VerifyCodeRequest
    ): ApiResponse<VerifyCodeResponse>

    @GET("api/parent/student-details")
    suspend fun getStudentDetails(
        @Header("Authorization") authHeader: String
    ): ApiResponse<StudentDetailsResponse>

    @POST("api/parent/device-token")
    suspend fun registerDeviceToken(
        @Header("Authorization") authHeader: String,
        @Body request: RegisterDeviceTokenRequest
    ): ApiResponse<Boolean>

    @GET("api/parent/notifications")
    suspend fun getNotifications(
        @Header("Authorization") authHeader: String
    ): ApiResponse<List<ParentNotificationResponse>>

    @POST("api/parent/notifications/{id}/read")
    suspend fun markNotificationAsRead(
        @Header("Authorization") authHeader: String,
        @retrofit2.http.Path("id") id: String
    ): ApiResponse<Boolean>

    @GET("api/parent/app-config")
    suspend fun getAppConfig(): ApiResponse<ParentAppConfigResponse>
}
