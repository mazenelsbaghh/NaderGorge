package com.nadergorge.paymentlistener.data.preference

import android.content.Context
import android.content.SharedPreferences
import android.util.Base64
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKeys
import com.google.gson.Gson
import com.google.gson.JsonParseException
import com.google.gson.reflect.TypeToken
import com.nadergorge.paymentlistener.service.SmsInboxCursor
import com.nadergorge.paymentlistener.service.SmsInboxReconciliationPolicy
import com.nadergorge.paymentlistener.service.SmsInboxReconciliationState
import java.security.MessageDigest
import java.security.SecureRandom
import java.util.Locale
import javax.crypto.Mac
import javax.crypto.spec.SecretKeySpec

private data class StoredWalletPairing(
    val token: String? = null,
    val subscriptionId: Int? = null,
    val simSlotIndex: Int? = null,
    val simLabel: String? = null,
    val carrierLabel: String? = null,
    val phoneNumber: String? = null,
    val label: String? = null,
    val smsSenderFilters: List<String?>? = null,
    val currentBalance: Double? = null,
    val dailyLimit: Double? = null,
    val monthlyLimit: Double? = null,
    val dailyReceived: Double? = null,
    val monthlyReceived: Double? = null,
    val isActive: Boolean? = null
) {
    fun toWalletPairing(): WalletPairing? = token?.let { storedToken ->
        WalletPairing(
            token = storedToken,
            subscriptionId = subscriptionId,
            simSlotIndex = simSlotIndex,
            simLabel = simLabel,
            carrierLabel = carrierLabel,
            phoneNumber = phoneNumber.orEmpty(),
            label = label.orEmpty(),
            smsSenderFilters = smsSenderFilters.orEmpty().filterNotNull(),
            currentBalance = currentBalance ?: 0.0,
            dailyLimit = dailyLimit ?: 0.0,
            monthlyLimit = monthlyLimit ?: 0.0,
            dailyReceived = dailyReceived ?: 0.0,
            monthlyReceived = monthlyReceived ?: 0.0,
            isActive = isActive ?: true
        )
    }

    companion object {
        fun from(pairing: WalletPairing): StoredWalletPairing = StoredWalletPairing(
            token = pairing.token,
            subscriptionId = pairing.subscriptionId,
            simSlotIndex = pairing.simSlotIndex,
            simLabel = pairing.simLabel,
            carrierLabel = pairing.carrierLabel,
            phoneNumber = pairing.phoneNumber,
            label = pairing.label,
            smsSenderFilters = pairing.smsSenderFilters,
            currentBalance = pairing.currentBalance,
            dailyLimit = pairing.dailyLimit,
            monthlyLimit = pairing.monthlyLimit,
            dailyReceived = pairing.dailyReceived,
            monthlyReceived = pairing.monthlyReceived,
            isActive = pairing.isActive
        )
    }
}

private data class StoredPendingSms(
    val receivedAtMillis: Long? = null,
    val messageId: Long? = null,
    val walletBindingKey: String? = null
) {
    fun toReference(): PendingSmsReference? {
        val timestamp = receivedAtMillis?.takeIf { value -> value >= 0L } ?: return null
        val id = messageId?.takeIf { value -> value >= 0L } ?: return null
        return PendingSmsReference(
            cursor = SmsInboxCursor(timestamp, id),
            walletBindingKey = walletBindingKey?.takeIf(String::isNotBlank)
        )
    }

    companion object {
        fun from(reference: PendingSmsReference): StoredPendingSms = StoredPendingSms(
            receivedAtMillis = reference.cursor.receivedAtMillis,
            messageId = reference.cursor.messageId,
            walletBindingKey = reference.walletBindingKey
        )
    }
}

private data class PendingSmsReference(
    val cursor: SmsInboxCursor,
    val walletBindingKey: String?
)

data class SmsReconciliationSnapshot(
    val pairings: List<WalletPairing>,
    val state: SmsInboxReconciliationState
)

class PreferenceManager(context: Context) {
    private val masterKeyAlias = MasterKeys.getOrCreate(MasterKeys.AES256_GCM_SPEC)
    private val sharedPreferences = EncryptedSharedPreferences.create(
        PREFERENCES_FILE,
        masterKeyAlias,
        context,
        EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
        EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM
    )
    private val gson = Gson()
    private val walletPairingListType = object : TypeToken<List<StoredWalletPairing?>>() {}.type
    private val pendingSmsListType = object : TypeToken<List<StoredPendingSms?>>() {}.type

    init {
        synchronized(PREFERENCES_LOCK) {
            migrateLegacyPairingLocked()
            if (readWalletPairingsLocked().isNotEmpty()) ensureQueueBindingSecretLocked()
        }
    }

    companion object {
        private const val PREFERENCES_FILE = "payment_listener_secure_prefs"
        private const val KEY_SERVER_URL = "server_url"
        private const val KEY_WALLET_PAIRINGS = "wallet_pairings"
        private const val KEY_QUEUE_BINDING_SECRET = "wallet_queue_binding_secret"
        private const val KEY_PAIRING_TOKEN = "pairing_token"
        private const val KEY_SMS_FILTERS = "sms_filters"
        private const val KEY_BALANCE = "last_balance"
        private const val KEY_PHONE = "device_phone"
        private const val KEY_LABEL = "device_label"
        private const val KEY_SMS_RECONCILIATION_TIMESTAMP = "sms_reconciliation_timestamp"
        private const val KEY_SMS_RECONCILIATION_MESSAGE_ID = "sms_reconciliation_message_id"
        private const val KEY_PENDING_SMS = "pending_sms"
        private const val KEY_SMS_FILTER_REVISION = "sms_filter_revision"
        private val LEGACY_DEFAULT_SMS_FILTERS = listOf("VF-Cash", "VodafoneCash")
        private const val QUEUE_BINDING_SECRET_BYTES = 32
        private const val QUEUE_BINDING_ALGORITHM = "HmacSHA256"
        private const val BASE64_FLAGS = Base64.NO_WRAP or Base64.NO_PADDING or Base64.URL_SAFE
        private val PREFERENCES_LOCK = Any()
    }

    fun getServerUrl(): String? = sharedPreferences.getString(KEY_SERVER_URL, null)

    fun saveServerUrl(url: String) {
        val sanitized = if (url.endsWith('/')) url.dropLast(1) else url
        sharedPreferences.edit()
            .putString(KEY_SERVER_URL, sanitized)
            .apply()
    }

    fun getWalletPairings(): List<WalletPairing> = synchronized(PREFERENCES_LOCK) {
        walletPairingSnapshot(readWalletPairingsLocked())
    }

    fun getWalletPairing(token: String): WalletPairing? = synchronized(PREFERENCES_LOCK) {
        val normalizedToken = normalizeToken(token)
        readWalletPairingsLocked()
            .singleOrNull { pairing -> pairing.token == normalizedToken }
            ?.let(::walletPairingSnapshot)
    }

    fun hasWalletPairings(): Boolean = synchronized(PREFERENCES_LOCK) {
        readWalletPairingsLocked().isNotEmpty()
    }

    fun hasWalletPairing(token: String): Boolean = synchronized(PREFERENCES_LOCK) {
        val normalizedToken = normalizeToken(token)
        readWalletPairingsLocked().any { pairing -> pairing.token == normalizedToken }
    }

    fun getWalletQueueBindingKey(token: String): String? = synchronized(PREFERENCES_LOCK) {
        val normalizedToken = normalizeToken(token)
        val pairing = readWalletPairingsLocked().singleOrNull { pairing ->
            pairing.token == normalizedToken
        } ?: return@synchronized null
        val secret = readQueueBindingSecretLocked() ?: return@synchronized null
        encodeQueueBinding(hmacQueueBinding(secret, pairing))
    }

    fun getWalletPairingByQueueBindingKey(bindingKey: String): WalletPairing? =
        synchronized(PREFERENCES_LOCK) {
            val expectedBinding = decodeQueueBinding(bindingKey) ?: return@synchronized null
            val secret = readQueueBindingSecretLocked() ?: return@synchronized null
            readWalletPairingsLocked()
                .singleOrNull { pairing ->
                    MessageDigest.isEqual(
                        hmacQueueBinding(secret, pairing),
                        expectedBinding
                    )
                }
                ?.let(::walletPairingSnapshot)
        }

    fun addWalletPairing(pairing: WalletPairing): WalletPairingWriteResult =
        synchronized(PREFERENCES_LOCK) {
            val candidate = requireValidPairing(pairing)
            val currentPairings = readWalletPairingsLocked()
            val existing = currentPairings.firstOrNull { stored -> stored.token == candidate.token }
            if (existing != null) {
                return@synchronized if (existing == candidate) {
                    WalletPairingWriteResult.UNCHANGED
                } else {
                    WalletPairingWriteResult.TOKEN_CONFLICT
                }
            }
            if (hasSimConflict(candidate, currentPairings)) {
                return@synchronized WalletPairingWriteResult.SIM_CONFLICT
            }

            persistWalletPairingsLocked(currentPairings, currentPairings + candidate)
            WalletPairingWriteResult.ADDED
        }

    fun updateWalletPairing(pairing: WalletPairing): WalletPairingWriteResult =
        synchronized(PREFERENCES_LOCK) {
            replaceWalletPairingLocked(requireValidPairing(pairing))
        }

    fun updateWalletSimBinding(
        token: String,
        binding: WalletSimBinding
    ): WalletPairingWriteResult = synchronized(PREFERENCES_LOCK) {
        val normalizedToken = normalizeToken(token)
        val existing = readWalletPairingsLocked()
            .singleOrNull { pairing -> pairing.token == normalizedToken }
            ?: return@synchronized WalletPairingWriteResult.NOT_FOUND
        replaceWalletPairingLocked(
            requireValidPairing(
                existing.copy(
                    subscriptionId = binding.subscriptionId,
                    simSlotIndex = binding.simSlotIndex,
                    simLabel = binding.simLabel,
                    carrierLabel = binding.carrierLabel
                )
            )
        )
    }

    fun updateWalletSync(
        token: String,
        snapshot: WalletSyncSnapshot
    ): WalletPairingWriteResult = synchronized(PREFERENCES_LOCK) {
        val normalizedToken = normalizeToken(token)
        val existing = readWalletPairingsLocked()
            .singleOrNull { pairing -> pairing.token == normalizedToken }
            ?: return@synchronized WalletPairingWriteResult.NOT_FOUND
        val refreshed = existing.copy(
            phoneNumber = snapshot.phoneNumber,
            label = snapshot.label,
            smsSenderFilters = snapshot.smsSenderFilters,
            currentBalance = snapshot.currentBalance,
            dailyLimit = snapshot.dailyLimit,
            monthlyLimit = snapshot.monthlyLimit,
            dailyReceived = snapshot.dailyReceived,
            monthlyReceived = snapshot.monthlyReceived,
            isActive = snapshot.isActive
        )
        replaceWalletPairingLocked(requireValidPairing(refreshed))
    }

    fun removeWalletPairing(token: String): Boolean = synchronized(PREFERENCES_LOCK) {
        val normalizedToken = normalizeToken(token)
        val currentPairings = readWalletPairingsLocked()
        val retainedPairings = currentPairings.filterNot { pairing -> pairing.token == normalizedToken }
        if (retainedPairings.size == currentPairings.size) return@synchronized false

        persistWalletPairingsLocked(currentPairings, retainedPairings)
        true
    }

    fun clearWalletPairings(): Boolean = synchronized(PREFERENCES_LOCK) {
        val currentPairings = readWalletPairingsLocked()
        if (currentPairings.isEmpty()) return@synchronized false

        persistWalletPairingsLocked(currentPairings, emptyList())
        true
    }

    fun clearConfiguration() {
        synchronized(PREFERENCES_LOCK) {
            val resetCursor = SmsInboxReconciliationPolicy.initialCursor(System.currentTimeMillis())
            val editor = sharedPreferences.edit()
                .remove(KEY_SERVER_URL)
                .remove(KEY_WALLET_PAIRINGS)
                .remove(KEY_QUEUE_BINDING_SECRET)
                .remove(KEY_PAIRING_TOKEN)
                .remove(KEY_SMS_FILTERS)
                .remove(KEY_BALANCE)
                .remove(KEY_PHONE)
                .remove(KEY_LABEL)
                .remove(KEY_PENDING_SMS)
            applyReconciliationResetLocked(editor, resetCursor)
            commitOrThrow(editor)
        }
    }

    fun getSmsReconciliationState(nowMillis: Long): SmsInboxReconciliationState =
        getSmsReconciliationSnapshot(nowMillis).state

    fun getSmsReconciliationSnapshot(nowMillis: Long): SmsReconciliationSnapshot =
        synchronized(PREFERENCES_LOCK) {
            val pairings = walletPairingSnapshot(readWalletPairingsLocked())
            SmsReconciliationSnapshot(
                pairings = pairings,
                state = SmsInboxReconciliationState(
                    cursor = readSmsReconciliationCursor(nowMillis),
                    filterRevision = sharedPreferences.getLong(KEY_SMS_FILTER_REVISION, 0L),
                    senderFilters = reconciliationFiltersLocked(pairings)
                )
            )
        }

    fun getWalletPairingsIfRevisionCurrent(expectedRevision: Long): List<WalletPairing>? =
        synchronized(PREFERENCES_LOCK) {
            if (!isFilterRevisionCurrentLocked(expectedRevision)) return@synchronized null
            walletPairingSnapshot(readWalletPairingsLocked())
        }

    fun ensureSmsReconciliationCursor(nowMillis: Long) {
        synchronized(PREFERENCES_LOCK) {
            if (sharedPreferences.contains(KEY_SMS_RECONCILIATION_TIMESTAMP)) {
                return@synchronized
            }

            val cursor = SmsInboxReconciliationPolicy.initialCursor(nowMillis)
            val editor = sharedPreferences.edit()
                .putLong(KEY_SMS_RECONCILIATION_TIMESTAMP, cursor.receivedAtMillis)
                .putLong(KEY_SMS_RECONCILIATION_MESSAGE_ID, cursor.messageId)
            commitOrThrow(editor)
        }
    }

    fun isSmsFilterRevisionCurrent(expectedRevision: Long): Boolean =
        synchronized(PREFERENCES_LOCK) {
            sharedPreferences.getLong(KEY_SMS_FILTER_REVISION, 0L) == expectedRevision
        }

    fun rememberPendingSms(
        expectedFilterRevision: Long,
        candidate: SmsInboxCursor,
        walletBindingKey: String?
    ): Boolean = synchronized(PREFERENCES_LOCK) {
        if (!isFilterRevisionCurrentLocked(expectedFilterRevision)) return@synchronized false
        val pending = readPendingSmsLocked()
        val candidateReference = PendingSmsReference(candidate, walletBindingKey)
        val updatedPending = pending.filterNot { reference -> reference.cursor == candidate } +
            candidateReference
        if (updatedPending == pending) return@synchronized true

        val editor = sharedPreferences.edit()
        persistPendingSmsLocked(editor, updatedPending)
        commitOrThrow(editor)
        true
    }

    fun clearPendingSms(
        expectedFilterRevision: Long,
        candidate: SmsInboxCursor
    ): Boolean = synchronized(PREFERENCES_LOCK) {
        if (!isFilterRevisionCurrentLocked(expectedFilterRevision)) return@synchronized false
        clearPendingSmsLocked(candidate)
    }

    fun completePendingSmsUpload(
        candidate: SmsInboxCursor,
        walletBindingKey: String
    ): Boolean = synchronized(PREFERENCES_LOCK) {
        val pending = readPendingSmsLocked()
        val completedReference = PendingSmsReference(candidate, walletBindingKey)
        if (completedReference !in pending) return@synchronized true
        val editor = sharedPreferences.edit()
        persistPendingSmsLocked(editor, pending - completedReference)
        commitOrThrow(editor)
        true
    }

    fun advanceSmsReconciliationCursor(
        expectedFilterRevision: Long,
        candidate: SmsInboxCursor
    ): Boolean = synchronized(PREFERENCES_LOCK) {
        if (sharedPreferences.getLong(KEY_SMS_FILTER_REVISION, 0L) != expectedFilterRevision) {
            return@synchronized false
        }

        val current = if (sharedPreferences.contains(KEY_SMS_RECONCILIATION_TIMESTAMP)) {
            readSmsReconciliationCursor(nowMillis = 0L)
        } else {
            null
        }
        val latest = current?.let { cursor ->
            SmsInboxReconciliationPolicy.latestCursor(cursor, candidate)
        } ?: candidate
        if (latest == current) return@synchronized true

        val editor = sharedPreferences.edit()
            .putLong(KEY_SMS_RECONCILIATION_TIMESTAMP, latest.receivedAtMillis)
            .putLong(KEY_SMS_RECONCILIATION_MESSAGE_ID, latest.messageId)
        commitOrThrow(editor)
        true
    }

    private fun replaceWalletPairingLocked(candidate: WalletPairing): WalletPairingWriteResult {
        val currentPairings = readWalletPairingsLocked()
        val index = currentPairings.indexOfFirst { pairing -> pairing.token == candidate.token }
        if (index < 0) return WalletPairingWriteResult.NOT_FOUND
        if (hasSimConflict(candidate, currentPairings, ignoredToken = candidate.token)) {
            return WalletPairingWriteResult.SIM_CONFLICT
        }
        if (currentPairings[index] == candidate) return WalletPairingWriteResult.UNCHANGED

        val updatedPairings = currentPairings.toMutableList().apply { set(index, candidate) }
        persistWalletPairingsLocked(currentPairings, updatedPairings)
        return WalletPairingWriteResult.UPDATED
    }

    private fun persistWalletPairingsLocked(
        currentPairings: List<WalletPairing>,
        updatedPairings: List<WalletPairing>
    ) {
        if (updatedPairings.isNotEmpty()) ensureQueueBindingSecretLocked()
        val editor = sharedPreferences.edit()
            .putString(
                KEY_WALLET_PAIRINGS,
                gson.toJson(updatedPairings.map(StoredWalletPairing::from))
            )
        if (updatedPairings.isEmpty()) editor.remove(KEY_QUEUE_BINDING_SECRET)
        if (requiresReconciliationReset(currentPairings, updatedPairings)) {
            val resetCursor = reconciliationResetCursorLocked(System.currentTimeMillis())
            applyReconciliationResetLocked(editor, resetCursor)
        }
        commitOrThrow(editor)
    }

    private fun requiresReconciliationReset(
        currentPairings: List<WalletPairing>,
        updatedPairings: List<WalletPairing>
    ): Boolean {
        val currentByToken = currentPairings.associateBy(WalletPairing::token)
        val updatedByToken = updatedPairings.associateBy(WalletPairing::token)
        if (currentByToken.keys != updatedByToken.keys) return true

        return currentByToken.any { (token, current) ->
            val updated = updatedByToken.getValue(token)
            current.subscriptionId != updated.subscriptionId ||
                current.simSlotIndex != updated.simSlotIndex ||
                !SmsInboxReconciliationPolicy.haveEquivalentFilters(
                    current.smsSenderFilters,
                    updated.smsSenderFilters
                )
        }
    }

    private fun reconciliationFiltersLocked(pairings: List<WalletPairing>): List<String> {
        return normalizeFilters(pairings.flatMap(WalletPairing::smsSenderFilters))
    }

    private fun readWalletPairingsLocked(): List<WalletPairing> {
        val json = sharedPreferences.getString(KEY_WALLET_PAIRINGS, null) ?: return emptyList()
        return try {
            val parsed: List<StoredWalletPairing?>? = gson.fromJson(json, walletPairingListType)
            parsed.orEmpty()
                .filterNotNull()
                .mapNotNull(StoredWalletPairing::toWalletPairing)
                .mapNotNull(::normalizeStoredPairing)
        } catch (_: JsonParseException) {
            emptyList()
        }
    }

    private fun migrateLegacyPairingLocked() {
        if (sharedPreferences.contains(KEY_WALLET_PAIRINGS)) return
        val legacyToken = sharedPreferences.getString(KEY_PAIRING_TOKEN, null)
            ?.let(::normalizeToken)
            ?.takeIf(String::isNotEmpty)
            ?: return
        val migratedPairing = legacyWalletPairingLocked(legacyToken)
        val resetCursor = SmsInboxReconciliationPolicy.initialCursor(System.currentTimeMillis())
        val editor = sharedPreferences.edit()
            .putString(
                KEY_WALLET_PAIRINGS,
                gson.toJson(listOf(StoredWalletPairing.from(migratedPairing)))
            )
            .putString(KEY_QUEUE_BINDING_SECRET, encodeQueueBinding(newQueueBindingSecret()))
        removeLegacyWalletValues(editor)
        if (sharedPreferences.contains(KEY_SMS_RECONCILIATION_TIMESTAMP)) {
            editor.putLong(
                KEY_SMS_FILTER_REVISION,
                nextFilterRevision(sharedPreferences.getLong(KEY_SMS_FILTER_REVISION, 0L))
            )
        } else {
            applyReconciliationResetLocked(editor, resetCursor)
        }
        commitOrThrow(editor)
    }

    private fun legacyWalletPairingLocked(token: String): WalletPairing = WalletPairing(
        token = token,
        phoneNumber = sharedPreferences.getString(KEY_PHONE, null).orEmpty(),
        label = sharedPreferences.getString(KEY_LABEL, null).orEmpty(),
        smsSenderFilters = parseLegacySmsFilters(sharedPreferences.getString(KEY_SMS_FILTERS, null)),
        currentBalance = sharedPreferences.getFloat(KEY_BALANCE, 0f).toDouble()
    )

    private fun removeLegacyWalletValues(editor: SharedPreferences.Editor) {
        editor.remove(KEY_PAIRING_TOKEN)
            .remove(KEY_SMS_FILTERS)
            .remove(KEY_BALANCE)
            .remove(KEY_PHONE)
            .remove(KEY_LABEL)
    }

    private fun parseLegacySmsFilters(json: String?): List<String> {
        if (json == null) return LEGACY_DEFAULT_SMS_FILTERS
        return try {
            val type = object : TypeToken<List<String?>>() {}.type
            val parsed: List<String?>? = gson.fromJson(json, type)
            normalizeFilters(parsed.orEmpty().filterNotNull())
        } catch (_: JsonParseException) {
            LEGACY_DEFAULT_SMS_FILTERS
        }
    }

    private fun requireValidPairing(pairing: WalletPairing): WalletPairing =
        normalizeStoredPairing(pairing)
            ?: throw IllegalArgumentException("Pairing token is required")

    private fun normalizeStoredPairing(pairing: WalletPairing): WalletPairing? {
        val normalizedToken = normalizeToken(pairing.token)
        if (normalizedToken.isEmpty()) return null
        return pairing.copy(
            token = normalizedToken,
            subscriptionId = pairing.subscriptionId?.takeIf { id -> id >= 0 },
            simSlotIndex = pairing.simSlotIndex?.takeIf { index -> index >= 0 },
            simLabel = pairing.simLabel?.trim()?.takeIf(String::isNotEmpty),
            carrierLabel = pairing.carrierLabel?.trim()?.takeIf(String::isNotEmpty),
            phoneNumber = pairing.phoneNumber.trim(),
            label = pairing.label.trim(),
            smsSenderFilters = normalizeFilters(pairing.smsSenderFilters)
        )
    }

    private fun hasSimConflict(
        candidate: WalletPairing,
        pairings: List<WalletPairing>,
        ignoredToken: String? = null
    ): Boolean = pairings.any { existing ->
        existing.token != ignoredToken &&
            ((candidate.subscriptionId != null && candidate.subscriptionId == existing.subscriptionId) ||
                (candidate.simSlotIndex != null && candidate.simSlotIndex == existing.simSlotIndex))
    }

    private fun normalizeFilters(filters: List<String>): List<String> {
        val normalizedNames = mutableSetOf<String>()
        return filters.map(String::trim)
            .filter(String::isNotEmpty)
            .filter { filter -> normalizedNames.add(filter.lowercase(Locale.ROOT)) }
    }

    private fun normalizeToken(token: String): String = token.trim().uppercase(Locale.ROOT)

    private fun ensureQueueBindingSecretLocked() {
        if (readQueueBindingSecretLocked() != null) return
        val editor = sharedPreferences.edit().putString(
            KEY_QUEUE_BINDING_SECRET,
            encodeQueueBinding(newQueueBindingSecret())
        )
        commitOrThrow(editor)
    }

    private fun readQueueBindingSecretLocked(): ByteArray? {
        val encodedSecret = sharedPreferences.getString(KEY_QUEUE_BINDING_SECRET, null)
            ?: return null
        return decodeQueueBinding(encodedSecret)
            ?.takeIf { secret -> secret.size == QUEUE_BINDING_SECRET_BYTES }
    }

    private fun newQueueBindingSecret(): ByteArray = ByteArray(QUEUE_BINDING_SECRET_BYTES).also(
        SecureRandom()::nextBytes
    )

    private fun hmacQueueBinding(secret: ByteArray, pairing: WalletPairing): ByteArray =
        Mac.getInstance(QUEUE_BINDING_ALGORITHM).run {
            init(SecretKeySpec(secret, QUEUE_BINDING_ALGORITHM))
            val bindingIdentity = listOf(
                pairing.token,
                pairing.subscriptionId?.toString().orEmpty(),
                pairing.simSlotIndex?.toString().orEmpty()
            ).joinToString("|")
            doFinal(bindingIdentity.toByteArray(Charsets.UTF_8))
        }

    private fun encodeQueueBinding(bytes: ByteArray): String =
        Base64.encodeToString(bytes, BASE64_FLAGS)

    private fun decodeQueueBinding(encoded: String): ByteArray? = try {
        Base64.decode(encoded, BASE64_FLAGS)
    } catch (_: IllegalArgumentException) {
        null
    }

    private fun walletPairingSnapshot(pairing: WalletPairing): WalletPairing =
        pairing.copy(smsSenderFilters = pairing.smsSenderFilters.toList())

    private fun walletPairingSnapshot(pairings: List<WalletPairing>): List<WalletPairing> =
        pairings.map(::walletPairingSnapshot)

    private fun applyReconciliationResetLocked(
        editor: SharedPreferences.Editor,
        resetCursor: SmsInboxCursor
    ) {
        editor.putLong(
            KEY_SMS_FILTER_REVISION,
            nextFilterRevision(sharedPreferences.getLong(KEY_SMS_FILTER_REVISION, 0L))
        )
            .putLong(KEY_SMS_RECONCILIATION_TIMESTAMP, resetCursor.receivedAtMillis)
            .putLong(KEY_SMS_RECONCILIATION_MESSAGE_ID, resetCursor.messageId)
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

    private fun readPendingSmsLocked(): List<PendingSmsReference> {
        val json = sharedPreferences.getString(KEY_PENDING_SMS, null) ?: return emptyList()
        return try {
            val parsed: List<StoredPendingSms?>? = gson.fromJson(json, pendingSmsListType)
            parsed.orEmpty()
                .filterNotNull()
                .mapNotNull(StoredPendingSms::toReference)
                .distinct()
                .sortedWith(
                    compareBy(
                        { reference: PendingSmsReference -> reference.cursor.receivedAtMillis },
                        { reference -> reference.cursor.messageId },
                        { reference -> reference.walletBindingKey.orEmpty() }
                    )
                )
        } catch (_: JsonParseException) {
            emptyList()
        }
    }

    private fun persistPendingSmsLocked(
        editor: SharedPreferences.Editor,
        pending: List<PendingSmsReference>
    ) {
        if (pending.isEmpty()) {
            editor.remove(KEY_PENDING_SMS)
        } else {
            editor.putString(
                KEY_PENDING_SMS,
                gson.toJson(pending.distinct().map(StoredPendingSms::from))
            )
        }
    }

    private fun clearPendingSmsLocked(candidate: SmsInboxCursor): Boolean {
        val pending = readPendingSmsLocked()
        val retained = pending.filterNot { reference -> reference.cursor == candidate }
        if (retained.size == pending.size) return true
        val editor = sharedPreferences.edit()
        persistPendingSmsLocked(editor, retained)
        commitOrThrow(editor)
        return true
    }

    private fun reconciliationResetCursorLocked(nowMillis: Long): SmsInboxCursor {
        val initialCursor = SmsInboxReconciliationPolicy.initialCursor(nowMillis)
        val unresolvedCursor = readPendingSmsLocked().firstOrNull()?.cursor ?: return initialCursor
        return SmsInboxReconciliationPolicy.earliestCursor(
            initialCursor,
            SmsInboxReconciliationPolicy.replayCursorBefore(unresolvedCursor)
        )
    }

    private fun isFilterRevisionCurrentLocked(expectedRevision: Long): Boolean =
        sharedPreferences.getLong(KEY_SMS_FILTER_REVISION, 0L) == expectedRevision

    private fun nextFilterRevision(current: Long): Long =
        if (current == Long.MAX_VALUE) 0L else current + 1L

    private fun commitOrThrow(editor: SharedPreferences.Editor) {
        check(editor.commit()) { "Unable to persist payment listener preferences" }
    }
}
