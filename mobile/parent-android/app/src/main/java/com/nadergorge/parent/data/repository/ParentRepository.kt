package com.nadergorge.parent.data.repository

import com.nadergorge.parent.data.api.ParentApiService
import com.nadergorge.parent.data.api.StudentDetailsResponse
import com.nadergorge.parent.data.api.VerifyCodeRequest
import com.nadergorge.parent.data.storage.LinkedStudent
import com.nadergorge.parent.data.storage.StorageService
import java.nio.charset.StandardCharsets
import retrofit2.HttpException
import com.google.gson.Gson
import java.io.IOException
import java.net.ConnectException
import java.net.UnknownHostException

class ParentRepository(
    private val apiService: ParentApiService,
    private val storageService: StorageService
) {
    private val gson = Gson()

    private fun handleException(e: Exception): Exception {
        return when (e) {
            is HttpException -> {
                try {
                    val errorBody = e.response()?.errorBody()?.string()
                    val apiResponse = gson.fromJson(errorBody, com.nadergorge.parent.data.api.ApiResponse::class.java)
                    Exception(apiResponse?.message ?: "حدث خطأ غير متوقع في الخادم")
                } catch (jsonEx: Exception) {
                    Exception("حدث خطأ في الخادم (رمز الاستجابة: ${e.code()})")
                }
            }
            is UnknownHostException,
            is ConnectException,
            is IOException -> {
                Exception("فشل الاتصال بالخادم، يرجى التحقق من اتصال الإنترنت")
            }
            else -> e
        }
    }

    suspend fun verifyAndLink(trackingCode: String, deviceToken: String): Result<LinkedStudent> {
        return try {
            val request = VerifyCodeRequest(trackingCode = trackingCode, deviceToken = deviceToken)
            val apiResponse = apiService.verifyCode(request)
            
            if (!apiResponse.success || apiResponse.data == null) {
                return Result.failure(Exception(apiResponse.message ?: "الرمز غير صالح، يرجى التحقق وإعادة المحاولة"))
            }
            
            val response = apiResponse.data
            val token = response.token
            val name = response.studentName
            
            // Extract studentId from response or token
            val studentId = response.studentId ?: extractStudentIdFromToken(token)
            
            if (studentId == null) {
                return Result.failure(Exception("Failed to extract student ID from token"))
            }
            
            val linkedStudent = LinkedStudent(studentId = studentId, name = name, token = token)
            storageService.addLinkedStudent(linkedStudent)
            
            Result.success(linkedStudent)
        } catch (e: Exception) {
            Result.failure(handleException(e))
        }
    }

    suspend fun verifyCodeOnly(trackingCode: String, deviceToken: String): Result<Pair<LinkedStudent, StudentDetailsResponse>> {
        return try {
            val request = VerifyCodeRequest(trackingCode = trackingCode, deviceToken = deviceToken)
            val apiResponse = apiService.verifyCode(request)
            
            if (!apiResponse.success || apiResponse.data == null) {
                return Result.failure(Exception(apiResponse.message ?: "الرمز غير صالح، يرجى التحقق وإعادة المحاولة"))
            }
            
            val response = apiResponse.data
            val token = response.token
            val name = response.studentName
            val studentId = response.studentId ?: extractStudentIdFromToken(token) ?: return Result.failure(Exception("Failed to extract student ID"))
            
            val linkedStudent = LinkedStudent(studentId = studentId, name = name, token = token)
            
            val authHeader = "Bearer $token"
            val detailsResponse = apiService.getStudentDetails(authHeader)
            
            if (!detailsResponse.success || detailsResponse.data == null) {
                return Result.failure(Exception(detailsResponse.message ?: "فشل في جلب بيانات الطالب للتحقق"))
            }
            
            Result.success(Pair(linkedStudent, detailsResponse.data))
        } catch (e: Exception) {
            Result.failure(handleException(e))
        }
    }

    fun saveLinkedStudent(linkedStudent: LinkedStudent) {
        storageService.addLinkedStudent(linkedStudent)
    }

    suspend fun getStudentDetails(studentId: String): Result<StudentDetailsResponse> {
        val student = storageService.getLinkedStudents().firstOrNull { it.studentId == studentId }
            ?: return Result.failure(Exception("Student not found locally"))
        return try {
            val authHeader = "Bearer ${student.token}"
            val apiResponse = apiService.getStudentDetails(authHeader)
            if (!apiResponse.success || apiResponse.data == null) {
                return Result.failure(Exception(apiResponse.message ?: "فشل تحميل البيانات"))
            }
            Result.success(apiResponse.data)
        } catch (e: Exception) {
            Result.failure(handleException(e))
        }
    }

    fun getLinkedStudents(): List<LinkedStudent> = storageService.getLinkedStudents()
    fun getActiveStudent(): LinkedStudent? = storageService.getActiveStudent()
    fun setActiveStudentId(studentId: String?) = storageService.setActiveStudentId(studentId)
    fun removeLinkedStudent(studentId: String) = storageService.removeLinkedStudent(studentId)

    fun extractStudentIdFromToken(token: String): String? {
        return try {
            val parts = token.split(".")
            if (parts.size < 2) return null
            val payloadBytes = java.util.Base64.getUrlDecoder().decode(parts[1])
            val payload = String(payloadBytes, StandardCharsets.UTF_8)
            
            val regex = "\"StudentId\"\\s*:\\s*\"([^\"]+)\"".toRegex(RegexOption.IGNORE_CASE)
            val match = regex.find(payload)
            match?.groupValues?.get(1) ?: run {
                val subRegex = "\"sub\"\\s*:\\s*\"([^\"]+)\"".toRegex(RegexOption.IGNORE_CASE)
                subRegex.find(payload)?.groupValues?.get(1)
            }
        } catch (e: Exception) {
            null
        }
    }
}
