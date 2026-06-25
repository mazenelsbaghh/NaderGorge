package com.nadergorge.paymentlistener.data.preference

import android.content.Context
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKeys
import com.google.gson.Gson
import com.google.gson.reflect.TypeToken

class PreferenceManager(context: Context) {
    private val masterKeyAlias = MasterKeys.getOrCreate(MasterKeys.AES256_GCM_SPEC)
    
    private val sharedPreferences = EncryptedSharedPreferences.create(
        "payment_listener_secure_prefs",
        masterKeyAlias,
        context,
        EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
        EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM
    )
    
    private val gson = Gson()

    companion object {
        private const val KEY_SERVER_URL = "server_url"
        private const val KEY_PAIRING_TOKEN = "pairing_token"
        private const val KEY_SMS_FILTERS = "sms_filters"
        private const val KEY_BALANCE = "last_balance"
        private const val KEY_PHONE = "device_phone"
        private const val KEY_LABEL = "device_label"
    }

    fun getServerUrl(): String? {
        return sharedPreferences.getString(KEY_SERVER_URL, null)
    }

    fun saveServerUrl(url: String) {
        // Ensure url ends without trailing slash for API calls
        val sanitized = if (url.endsWith("/")) url.substring(0, url.length - 1) else url
        sharedPreferences.edit().putString(KEY_SERVER_URL, sanitized).apply()
    }

    fun getPairingToken(): String? {
        return sharedPreferences.getString(KEY_PAIRING_TOKEN, null)
    }

    fun savePairingToken(token: String) {
        sharedPreferences.edit().putString(KEY_PAIRING_TOKEN, token).apply()
    }

    fun clearConfiguration() {
        sharedPreferences.edit()
            .remove(KEY_SERVER_URL)
            .remove(KEY_PAIRING_TOKEN)
            .remove(KEY_SMS_FILTERS)
            .remove(KEY_BALANCE)
            .remove(KEY_PHONE)
            .remove(KEY_LABEL)
            .apply()
    }

    fun getSmsFilters(): List<String> {
        val json = sharedPreferences.getString(KEY_SMS_FILTERS, null) ?: return listOf("VodafoneCash")
        return try {
            val type = object : TypeToken<List<String>>() {}.type
            gson.fromJson(json, type) ?: listOf("VodafoneCash")
        } catch (e: Exception) {
            listOf("VodafoneCash")
        }
    }

    fun saveSmsFilters(filters: List<String>) {
        val json = gson.toJson(filters)
        sharedPreferences.edit().putString(KEY_SMS_FILTERS, json).apply()
    }

    fun getLastBalance(): Float {
        return sharedPreferences.getFloat(KEY_BALANCE, 0f)
    }

    fun saveLastBalance(balance: Float) {
        sharedPreferences.edit().putFloat(KEY_BALANCE, balance).apply()
    }

    fun getDevicePhone(): String? {
        return sharedPreferences.getString(KEY_PHONE, null)
    }

    fun saveDevicePhone(phone: String) {
        sharedPreferences.edit().putString(KEY_PHONE, phone).apply()
    }

    fun getDeviceLabel(): String? {
        return sharedPreferences.getString(KEY_LABEL, null)
    }

    fun saveDeviceLabel(label: String) {
        sharedPreferences.edit().putString(KEY_LABEL, label).apply()
    }
}
