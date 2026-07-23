package com.nadergorge.parent.data.api

data class VerifyCodeRequest(
    val trackingCode: String,
    val deviceToken: String,
    val platform: String = "android"
)

data class RegisterDeviceTokenRequest(
    val deviceToken: String,
    val platform: String = "android"
)
