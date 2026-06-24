package com.nadergorge.parent.data.api

data class VerifyCodeResponse(
    val token: String,
    val studentName: String,
    val studentId: String? = null
)
