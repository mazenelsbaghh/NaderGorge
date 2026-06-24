package com.nadergorge.parent.data.repository

import com.nadergorge.parent.data.api.ParentApiService
import com.nadergorge.parent.data.api.StudentDetailsResponse
import com.nadergorge.parent.data.api.VerifyCodeRequest
import com.nadergorge.parent.data.storage.LinkedStudent
import com.nadergorge.parent.data.storage.StorageService
import java.nio.charset.StandardCharsets

class ParentRepository(
    private val apiService: ParentApiService,
    private val storageService: StorageService
) {
    suspend fun verifyAndLink(trackingCode: String, deviceToken: String): Result<LinkedStudent> {
        return try {
            val request = VerifyCodeRequest(trackingCode = trackingCode, deviceToken = deviceToken)
            val response = apiService.verifyCode(request)
            
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
            Result.failure(e)
        }
    }

    suspend fun getStudentDetails(studentId: String): Result<StudentDetailsResponse> {
        val student = storageService.getLinkedStudents().firstOrNull { it.studentId == studentId }
            ?: return Result.failure(Exception("Student not found locally"))
        return try {
            val authHeader = "Bearer ${student.token}"
            val details = apiService.getStudentDetails(authHeader)
            Result.success(details)
        } catch (e: Exception) {
            Result.failure(e)
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
