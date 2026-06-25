package com.nadergorge.parent.data.api

data class ApiResponse<T>(
    val success: Boolean,
    val data: T?,
    val message: String?,
    val errors: List<String>?
)
