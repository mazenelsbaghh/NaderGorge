package com.nadergorge.parent.data.api

data class StudentDetailsResponse(
    val studentName: String,
    val grade: String,
    val school: String,
    val avatarSlug: String?,
    val attendance: AttendanceInfo,
    val exams: List<ExamInfo>,
    val homeworks: List<HomeworkInfo>,
    val warnings: List<WarningInfo>
)

data class AttendanceInfo(
    val totalLessons: Int,
    val watchedLessons: Int,
    val completionRate: Double
)

data class ExamInfo(
    val examTitle: String,
    val score: Int,
    val totalScore: Int,
    val percentage: Double,
    val submittedAt: String,
    val status: String // e.g. "Passed" or "Failed"
)

data class HomeworkInfo(
    val title: String,
    val isSubmitted: Boolean,
    val grade: String?,
    val submittedAt: String?
)

data class WarningInfo(
    val reason: String,
    val severity: String, // e.g. "High", "Medium", "Low"
    val createdAt: String
)
