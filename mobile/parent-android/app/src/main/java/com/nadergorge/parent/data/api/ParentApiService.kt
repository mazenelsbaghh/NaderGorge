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
}
