package com.nadergorge.paymentlistener.data.api

import android.content.Context
import com.nadergorge.paymentlistener.BuildConfig
import com.nadergorge.paymentlistener.data.preference.PreferenceManager
import okhttp3.OkHttpClient
import okhttp3.logging.HttpLoggingInterceptor
import retrofit2.Retrofit
import retrofit2.converter.gson.GsonConverterFactory
import java.util.concurrent.TimeUnit

object ApiClient {
    private var retrofit: Retrofit? = null
    private var lastUrl: String? = null

    @Synchronized
    fun getApiService(context: Context): ApiService? {
        val prefManager = PreferenceManager(context)
        val url = prefManager.getServerUrl() ?: return null
        
        val sanitizedUrl = normalizeBaseUrl(url)

        if (retrofit == null || lastUrl != sanitizedUrl) {
            val logging = HttpLoggingInterceptor().apply {
                level = if (BuildConfig.DEBUG) {
                    HttpLoggingInterceptor.Level.BASIC
                } else {
                    HttpLoggingInterceptor.Level.NONE
                }
            }

            val okHttpClient = OkHttpClient.Builder()
                .addInterceptor(logging)
                .connectTimeout(15, TimeUnit.SECONDS)
                .readTimeout(15, TimeUnit.SECONDS)
                .writeTimeout(15, TimeUnit.SECONDS)
                .build()

            retrofit = Retrofit.Builder()
                .baseUrl(sanitizedUrl)
                .client(okHttpClient)
                .addConverterFactory(GsonConverterFactory.create())
                .build()
            
            lastUrl = sanitizedUrl
        }

        return retrofit?.create(ApiService::class.java)
    }

    fun normalizeBaseUrl(rawUrl: String): String {
        var url = rawUrl.trim()
        if (url.isBlank()) return url

        if (!url.startsWith("http://", ignoreCase = true) && !url.startsWith("https://", ignoreCase = true)) {
            val host = url.substringBefore("/")
            val scheme = if (isLocalHost(host)) "http" else "https"
            url = "$scheme://$url"
        }

        url = url.trimEnd('/')
        if (url.endsWith("/api", ignoreCase = true)) {
            url = url.dropLast(4).trimEnd('/')
        }

        return "$url/"
    }

    private fun isLocalHost(hostWithPort: String): Boolean {
        val host = hostWithPort.substringBefore(":").lowercase()
        return host == "localhost" ||
            host == "127.0.0.1" ||
            host.startsWith("10.") ||
            host.startsWith("192.168.") ||
            Regex("^172\\.(1[6-9]|2\\d|3[0-1])\\.").containsMatchIn(host)
    }
}
