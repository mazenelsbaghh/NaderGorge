package com.nadergorge.paymentlistener.data.preference

import android.content.Context
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKeys
import com.google.gson.Gson
import com.google.gson.reflect.TypeToken
import com.nadergorge.paymentlistener.service.SmsInboxCursor
import com.nadergorge.paymentlistener.service.SmsInboxReconciliationPolicy
import com.nadergorge.paymentlistener.service.SmsInboxReconciliationState

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
        private const val KEY_SMS_RECONCILIATION_TIMESTAMP = "sms_reconciliation_timestamp"
        private const val KEY_SMS_RECONCILIATION_MESSAGE_ID = "sms_reconciliation_message_id"
        private const val KEY_SMS_FILTER_REVISION = "sms_filter_revision"
        private val DEFAULT_SMS_FILTERS = listOf("VF-Cash", "VodafoneCash")
        private val SMS_RECONCILIATION_LOCK = Any()
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
        synchronized(SMS_RECONCILIATION_LOCK) {
            val nextRevision = nextFilterRevision(
                sharedPreferences.getLong(KEY_SMS_FILTER_REVISION, 0L)
            )
            sharedPreferences.edit()
                .remove(KEY_SERVER_URL)
                .remove(KEY_PAIRING_TOKEN)
                .remove(KEY_SMS_FILTERS)
                .remove(KEY_BALANCE)
                .remove(KEY_PHONE)
                .remove(KEY_LABEL)
                .remove(KEY_SMS_RECONCILIATION_TIMESTAMP)
                .remove(KEY_SMS_RECONCILIATION_MESSAGE_ID)
                .putLong(KEY_SMS_FILTER_REVISION, nextRevision)
                .commit()
        }
    }

    fun getSmsFilters(): List<String> {
        return parseSmsFilters(sharedPreferences.getString(KEY_SMS_FILTERS, null))
    }

    private fun parseSmsFilters(json: String?): List<String> {
        if (json == null) return DEFAULT_SMS_FILTERS
        return try {
            val type = object : TypeToken<List<String?>>() {}.type
            val parsedFilters: List<String?>? = gson.fromJson(json, type)
            parsedFilters?.filterNotNull() ?: DEFAULT_SMS_FILTERS
        } catch (_: Exception) {
            DEFAULT_SMS_FILTERS
        }
    }

    fun saveSmsFilters(filters: List<String>) {
        synchronized(SMS_RECONCILIATION_LOCK) {
            val storedJson = sharedPreferences.getString(KEY_SMS_FILTERS, null)
            val filtersChanged = !SmsInboxReconciliationPolicy.haveEquivalentFilters(
                parseSmsFilters(storedJson),
                filters
            )
            if (!filtersChanged) return@synchronized

            val resetCursor = SmsInboxReconciliationPolicy.initialCursor(System.currentTimeMillis())
            sharedPreferences.edit()
                .putString(KEY_SMS_FILTERS, gson.toJson(filters))
                .putLong(
                    KEY_SMS_FILTER_REVISION,
                    nextFilterRevision(sharedPreferences.getLong(KEY_SMS_FILTER_REVISION, 0L))
                )
                .putLong(KEY_SMS_RECONCILIATION_TIMESTAMP, resetCursor.receivedAtMillis)
                .putLong(KEY_SMS_RECONCILIATION_MESSAGE_ID, resetCursor.messageId)
                .commit()
        }
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

    fun getSmsReconciliationState(nowMillis: Long): SmsInboxReconciliationState {
        return synchronized(SMS_RECONCILIATION_LOCK) {
            SmsInboxReconciliationState(
                cursor = readSmsReconciliationCursor(nowMillis),
                filterRevision = sharedPreferences.getLong(KEY_SMS_FILTER_REVISION, 0L),
                senderFilters = parseSmsFilters(sharedPreferences.getString(KEY_SMS_FILTERS, null))
            )
        }
    }

    fun ensureSmsReconciliationCursor(nowMillis: Long) {
        synchronized(SMS_RECONCILIATION_LOCK) {
            if (sharedPreferences.contains(KEY_SMS_RECONCILIATION_TIMESTAMP)) {
                return@synchronized
            }

            val cursor = SmsInboxReconciliationPolicy.initialCursor(nowMillis)
            sharedPreferences.edit()
                .putLong(KEY_SMS_RECONCILIATION_TIMESTAMP, cursor.receivedAtMillis)
                .putLong(KEY_SMS_RECONCILIATION_MESSAGE_ID, cursor.messageId)
                .commit()
        }
    }

    fun isSmsFilterRevisionCurrent(expectedRevision: Long): Boolean {
        return synchronized(SMS_RECONCILIATION_LOCK) {
            sharedPreferences.getLong(KEY_SMS_FILTER_REVISION, 0L) == expectedRevision
        }
    }

    fun advanceSmsReconciliationCursor(
        expectedFilterRevision: Long,
        candidate: SmsInboxCursor
    ): Boolean {
        return synchronized(SMS_RECONCILIATION_LOCK) {
            if (sharedPreferences.getLong(KEY_SMS_FILTER_REVISION, 0L) != expectedFilterRevision) {
                return@synchronized false
            }

            val current = if (sharedPreferences.contains(KEY_SMS_RECONCILIATION_TIMESTAMP)) {
                readSmsReconciliationCursor(nowMillis = 0L)
            } else {
                null
            }
            val latest = current?.let {
                SmsInboxReconciliationPolicy.latestCursor(it, candidate)
            } ?: candidate
            if (latest == current) return@synchronized true

            sharedPreferences.edit()
                .putLong(KEY_SMS_RECONCILIATION_TIMESTAMP, latest.receivedAtMillis)
                .putLong(KEY_SMS_RECONCILIATION_MESSAGE_ID, latest.messageId)
                .commit()
        }
    }

    private fun readSmsReconciliationCursor(nowMillis: Long): SmsInboxCursor {
        if (!sharedPreferences.contains(KEY_SMS_RECONCILIATION_TIMESTAMP)) {
            return SmsInboxReconciliationPolicy.initialCursor(nowMillis)
        }

        return SmsInboxCursor(
            receivedAtMillis = sharedPreferences.getLong(KEY_SMS_RECONCILIATION_TIMESTAMP, 0L),
            messageId = sharedPreferences.getLong(KEY_SMS_RECONCILIATION_MESSAGE_ID, -1L)
        )
    }

    private fun nextFilterRevision(current: Long): Long {
        return if (current == Long.MAX_VALUE) 0L else current + 1L
    }
}
