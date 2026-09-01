package com.nadergorge.paymentlistener.data.preference

import android.content.Context
import android.content.SharedPreferences
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKeys
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import com.nadergorge.paymentlistener.service.SmsInboxCursor
import com.nadergorge.paymentlistener.service.SmsInboxReconciliationPolicy
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class PreferenceManagerReconciliationTest {
    private lateinit var context: Context
    private lateinit var preferences: PreferenceManager

    @Before
    fun resetPreferences() {
        context = InstrumentationRegistry.getInstrumentation().context
        preferences = PreferenceManager(context)
        preferences.clearConfiguration()
    }

    @After
    fun clearPreferences() {
        preferences.clearConfiguration()
    }

    @Test
    fun changedSenderFiltersResetCursorAndRejectOldWorkerRevision() {
        val nowMillis = 1_787_673_840_000L
        val pairing = pairing(
            token = "TOKENA01",
            subscriptionId = 10,
            slotIndex = 0,
            filters = listOf("VF-Cash")
        )
        assertEquals(WalletPairingWriteResult.ADDED, preferences.addWalletPairing(pairing))
        val initialState = preferences.getSmsReconciliationState(nowMillis)
        val uploadedCursor = SmsInboxCursor(nowMillis - 1_000L, 7L)
        assertTrue(
            preferences.advanceSmsReconciliationCursor(
                initialState.filterRevision,
                uploadedCursor
            )
        )

        assertEquals(
            WalletPairingWriteResult.UPDATED,
            preferences.updateWalletPairing(pairing.copy(smsSenderFilters = listOf("OrangeCash")))
        )
        val refreshedState = preferences.getSmsReconciliationState(nowMillis)

        assertNotEquals(initialState.filterRevision, refreshedState.filterRevision)
        assertNotEquals(uploadedCursor, refreshedState.cursor)
        assertEquals(-1L, refreshedState.cursor.messageId)
        assertFalse(
            preferences.advanceSmsReconciliationCursor(
                initialState.filterRevision,
                SmsInboxCursor(nowMillis, 8L)
            )
        )
    }

    @Test
    fun olderWorkerCursorCannotOverwriteNewerCheckpoint() {
        val nowMillis = 1_787_673_840_000L
        val state = preferences.getSmsReconciliationState(nowMillis)
        val newerCursor = SmsInboxCursor(nowMillis - 1_000L, 20L)
        val olderCursor = SmsInboxCursor(nowMillis - 2_000L, 99L)

        assertTrue(preferences.advanceSmsReconciliationCursor(state.filterRevision, newerCursor))
        assertTrue(preferences.advanceSmsReconciliationCursor(state.filterRevision, olderCursor))

        assertEquals(
            newerCursor,
            preferences.getSmsReconciliationState(nowMillis).cursor
        )
    }

    @Test
    fun equivalentFilterRefreshKeepsExistingCheckpoint() {
        val nowMillis = 1_787_673_840_000L
        val pairing = pairing(
            token = "TOKENA01",
            subscriptionId = 10,
            slotIndex = 0,
            filters = listOf("VF-Cash", "OrangeCash")
        )
        assertEquals(WalletPairingWriteResult.ADDED, preferences.addWalletPairing(pairing))
        val state = preferences.getSmsReconciliationState(nowMillis)
        val uploadedCursor = SmsInboxCursor(nowMillis - 1_000L, 20L)
        assertTrue(preferences.advanceSmsReconciliationCursor(state.filterRevision, uploadedCursor))

        assertEquals(
            WalletPairingWriteResult.UPDATED,
            preferences.updateWalletPairing(
                pairing.copy(smsSenderFilters = listOf(" orangecash ", "vf-cash"))
            )
        )
        val refreshedState = preferences.getSmsReconciliationState(nowMillis)

        assertEquals(state.filterRevision, refreshedState.filterRevision)
        assertEquals(uploadedCursor, refreshedState.cursor)
    }

    @Test
    fun initialCheckpointUsesScheduleTimeAndIsNotMovedByLaterScheduling() {
        val firstScheduleAt = 1_787_673_840_000L
        val laterScheduleAt = firstScheduleAt + SmsInboxReconciliationPolicy.INITIAL_LOOKBACK_MILLIS

        preferences.ensureSmsReconciliationCursor(firstScheduleAt)
        preferences.ensureSmsReconciliationCursor(laterScheduleAt)

        assertEquals(
            SmsInboxReconciliationPolicy.initialCursor(firstScheduleAt),
            preferences.getSmsReconciliationState(laterScheduleAt).cursor
        )
    }

    @Test
    fun twoWalletsPersistAndExposeFilterUnion() {
        assertEquals(
            WalletPairingWriteResult.ADDED,
            preferences.addWalletPairing(
                pairing("TOKENA01", subscriptionId = 10, slotIndex = 0, filters = listOf("VF-Cash"))
            )
        )
        assertEquals(
            WalletPairingWriteResult.ADDED,
            preferences.addWalletPairing(
                pairing(
                    "TOKENB02",
                    subscriptionId = 20,
                    slotIndex = 1,
                    filters = listOf("OrangeCash", "vf-cash")
                )
            )
        )

        assertEquals(setOf("TOKENA01", "TOKENB02"), preferences.getWalletPairings().map { it.token }.toSet())
        assertTrue(preferences.hasWalletPairings())
        assertTrue(preferences.hasWalletPairing("tokenb02"))
        assertEquals(
            listOf("VF-Cash", "OrangeCash"),
            preferences.getSmsReconciliationState(System.currentTimeMillis()).senderFilters
        )
    }

    @Test
    fun existingTokenCannotBeSilentlyReboundOrOverwritten() {
        val stored = pairing("TOKENA01", subscriptionId = 10, slotIndex = 0)
        assertEquals(WalletPairingWriteResult.ADDED, preferences.addWalletPairing(stored))

        assertEquals(WalletPairingWriteResult.UNCHANGED, preferences.addWalletPairing(stored))
        assertEquals(
            WalletPairingWriteResult.TOKEN_CONFLICT,
            preferences.addWalletPairing(stored.copy(subscriptionId = 20, simSlotIndex = 1))
        )
        assertEquals(stored, preferences.getWalletPairing(stored.token))
    }

    @Test
    fun secondWalletCannotClaimAnExistingSubscriptionOrSlot() {
        assertEquals(
            WalletPairingWriteResult.ADDED,
            preferences.addWalletPairing(pairing("TOKENA01", subscriptionId = 10, slotIndex = 0))
        )

        val conflictingPairings = listOf(
            pairing("TOKENB02", subscriptionId = 10, slotIndex = 1),
            pairing("TOKENC03", subscriptionId = 30, slotIndex = 0)
        )
        conflictingPairings.forEach { conflicting ->
            assertEquals(
                WalletPairingWriteResult.SIM_CONFLICT,
                preferences.addWalletPairing(conflicting)
            )
        }
        assertEquals(1, preferences.getWalletPairings().size)
    }

    @Test
    fun syncMetadataKeepsCursorUntilSenderFiltersChange() {
        val nowMillis = System.currentTimeMillis()
        val token = "TOKENA01"
        preferences.addWalletPairing(
            pairing(token, subscriptionId = 10, slotIndex = 0, filters = listOf("VF-Cash"))
        )
        val initialState = preferences.getSmsReconciliationState(nowMillis)
        val uploadedCursor = SmsInboxCursor(nowMillis - 1_000L, 42L)
        assertTrue(preferences.advanceSmsReconciliationCursor(initialState.filterRevision, uploadedCursor))

        assertEquals(
            WalletPairingWriteResult.UPDATED,
            preferences.updateWalletSync(token, syncSnapshot(filters = listOf(" vf-cash ")))
        )
        val metadataState = preferences.getSmsReconciliationState(nowMillis)
        assertEquals(initialState.filterRevision, metadataState.filterRevision)
        assertEquals(uploadedCursor, metadataState.cursor)

        assertEquals(
            WalletPairingWriteResult.UPDATED,
            preferences.updateWalletSync(token, syncSnapshot(filters = listOf("OrangeCash")))
        )
        val filterState = preferences.getSmsReconciliationState(nowMillis)
        assertNotEquals(metadataState.filterRevision, filterState.filterRevision)
        assertEquals(-1L, filterState.cursor.messageId)
        assertEquals(listOf("OrangeCash"), filterState.senderFilters)
    }

    @Test
    fun changingSimBindingInvalidatesRunningReconciliation() {
        val nowMillis = System.currentTimeMillis()
        val original = pairing("TOKENA01", subscriptionId = 10, slotIndex = 0)
        preferences.addWalletPairing(original)
        val initialState = preferences.getSmsReconciliationState(nowMillis)
        val uploadedCursor = SmsInboxCursor(nowMillis - 1_000L, 7L)
        assertTrue(preferences.advanceSmsReconciliationCursor(initialState.filterRevision, uploadedCursor))

        assertEquals(
            WalletPairingWriteResult.UPDATED,
            preferences.updateWalletPairing(original.copy(subscriptionId = 20, simSlotIndex = 1))
        )
        val reboundState = preferences.getSmsReconciliationState(nowMillis)

        assertNotEquals(initialState.filterRevision, reboundState.filterRevision)
        assertNotEquals(uploadedCursor, reboundState.cursor)
        assertEquals(-1L, reboundState.cursor.messageId)
    }

    @Test
    fun oldUnresolvedMessageIsReplayedAfterExplicitSimRebinding() {
        val nowMillis = System.currentTimeMillis()
        val original = pairing("TOKENA01", subscriptionId = 10, slotIndex = 0)
        preferences.addWalletPairing(original)
        val state = preferences.getSmsReconciliationState(nowMillis)
        val unresolved = SmsInboxCursor(
            receivedAtMillis = nowMillis -
                (SmsInboxReconciliationPolicy.INITIAL_LOOKBACK_MILLIS * 3),
            messageId = 70L
        )
        assertTrue(
            preferences.rememberPendingSms(
                state.filterRevision,
                unresolved,
                walletBindingKey = null
            )
        )

        assertEquals(
            WalletPairingWriteResult.UPDATED,
            preferences.updateWalletSimBinding(
                original.token,
                WalletSimBinding(20, 1, "SIM 2", "Carrier 20")
            )
        )

        assertEquals(
            SmsInboxReconciliationPolicy.replayCursorBefore(unresolved),
            preferences.getSmsReconciliationState(nowMillis).cursor
        )
    }

    @Test
    fun simBindingUpdatePreservesTheLatestWalletMetadata() {
        val token = "TOKENA01"
        val original = pairing(token, subscriptionId = 10, slotIndex = 0)
        assertEquals(WalletPairingWriteResult.ADDED, preferences.addWalletPairing(original))
        assertEquals(
            WalletPairingWriteResult.UPDATED,
            preferences.updateWalletSync(token, syncSnapshot(filters = listOf("OrangeCash")))
        )
        val syncedWallet = preferences.getWalletPairing(token)!!

        assertEquals(
            WalletPairingWriteResult.UPDATED,
            preferences.updateWalletSimBinding(
                token,
                WalletSimBinding(
                    subscriptionId = 20,
                    simSlotIndex = 1,
                    simLabel = "SIM 2",
                    carrierLabel = "Carrier 20"
                )
            )
        )
        val reboundWallet = preferences.getWalletPairing(token)!!

        assertEquals(syncedWallet.phoneNumber, reboundWallet.phoneNumber)
        assertEquals(syncedWallet.label, reboundWallet.label)
        assertEquals(syncedWallet.smsSenderFilters, reboundWallet.smsSenderFilters)
        assertEquals(syncedWallet.currentBalance, reboundWallet.currentBalance, 0.001)
        assertEquals(20, reboundWallet.subscriptionId)
        assertEquals(1, reboundWallet.simSlotIndex)
    }

    @Test
    fun removingOneWalletKeepsTheOtherWalletPaired() {
        val first = pairing("TOKENA01", subscriptionId = 10, slotIndex = 0)
        val second = pairing("TOKENB02", subscriptionId = 20, slotIndex = 1)
        preferences.addWalletPairing(first)
        preferences.addWalletPairing(second)

        assertTrue(preferences.removeWalletPairing(first.token))

        assertNull(preferences.getWalletPairing(first.token))
        assertEquals(second, preferences.getWalletPairing(second.token))
        assertTrue(preferences.hasWalletPairings())
    }

    @Test
    fun queueBindingIsOpaqueAndStopsResolvingAfterWalletRemoval() {
        val first = pairing("TOKENA01", subscriptionId = 10, slotIndex = 0)
        val second = pairing("TOKENB02", subscriptionId = 20, slotIndex = 1)
        preferences.addWalletPairing(first)
        preferences.addWalletPairing(second)

        val firstBinding = preferences.getWalletQueueBindingKey(first.token)!!
        val secondBinding = preferences.getWalletQueueBindingKey(second.token)!!

        assertFalse(firstBinding.contains(first.token, ignoreCase = true))
        assertNotEquals(firstBinding, secondBinding)
        assertEquals(first, preferences.getWalletPairingByQueueBindingKey(firstBinding))
        assertTrue(preferences.removeWalletPairing(first.token))
        assertNull(preferences.getWalletPairingByQueueBindingKey(firstBinding))
        assertEquals(second, preferences.getWalletPairingByQueueBindingKey(secondBinding))
    }

    @Test
    fun simRebindingInvalidatesPreviouslyQueuedWalletBinding() {
        val pairing = pairing("TOKENA01", subscriptionId = 10, slotIndex = 0)
        preferences.addWalletPairing(pairing)
        val oldBinding = preferences.getWalletQueueBindingKey(pairing.token)!!

        assertEquals(
            WalletPairingWriteResult.UPDATED,
            preferences.updateWalletSimBinding(
                pairing.token,
                WalletSimBinding(20, 1, "SIM 2", "Carrier 20")
            )
        )

        assertNull(preferences.getWalletPairingByQueueBindingKey(oldBinding))
        val newBinding = preferences.getWalletQueueBindingKey(pairing.token)!!
        assertNotEquals(oldBinding, newBinding)
    }

    @Test
    fun staleReconciliationRevisionCannotReadPairingsAfterRebinding() {
        val pairing = pairing("TOKENA01", subscriptionId = 10, slotIndex = 0)
        preferences.addWalletPairing(pairing)
        val snapshot = preferences.getSmsReconciliationSnapshot(System.currentTimeMillis())

        preferences.updateWalletSimBinding(
            pairing.token,
            WalletSimBinding(20, 1, "SIM 2", "Carrier 20")
        )

        assertNull(
            preferences.getWalletPairingsIfRevisionCurrent(snapshot.state.filterRevision)
        )
    }

    @Test
    fun completingEarliestPendingSmsKeepsLaterPendingReplayFloor() {
        val nowMillis = System.currentTimeMillis()
        val pairing = pairing("TOKENA01", subscriptionId = 10, slotIndex = 0)
        preferences.addWalletPairing(pairing)
        val state = preferences.getSmsReconciliationState(nowMillis)
        val first = SmsInboxCursor(nowMillis - 500_000L, 10L)
        val second = SmsInboxCursor(nowMillis - 400_000L, 20L)
        assertTrue(preferences.rememberPendingSms(state.filterRevision, first, "binding-a"))
        assertTrue(preferences.rememberPendingSms(state.filterRevision, second, "binding-b"))
        assertTrue(preferences.completePendingSmsUpload(first, "binding-a"))

        preferences.updateWalletSimBinding(
            pairing.token,
            WalletSimBinding(20, 1, "SIM 2", "Carrier 20")
        )

        assertEquals(
            SmsInboxReconciliationPolicy.replayCursorBefore(second),
            preferences.getSmsReconciliationState(nowMillis).cursor
        )
    }

    @Test
    fun oldQueueCompletionCannotDeleteReboundPendingGeneration() {
        val nowMillis = System.currentTimeMillis()
        val original = pairing("TOKENA01", subscriptionId = 10, slotIndex = 0)
        preferences.addWalletPairing(original)
        val oldState = preferences.getSmsReconciliationState(nowMillis)
        val message = SmsInboxCursor(
            nowMillis - (SmsInboxReconciliationPolicy.INITIAL_LOOKBACK_MILLIS * 3),
            90L
        )
        val oldBinding = preferences.getWalletQueueBindingKey(original.token)!!
        assertTrue(preferences.rememberPendingSms(oldState.filterRevision, message, oldBinding))
        assertTrue(preferences.advanceSmsReconciliationCursor(oldState.filterRevision, message))

        assertEquals(
            WalletPairingWriteResult.UPDATED,
            preferences.updateWalletSimBinding(
                original.token,
                WalletSimBinding(20, 1, "SIM 2", "Carrier 20")
            )
        )
        val reboundState = preferences.getSmsReconciliationState(nowMillis)
        val reboundBinding = preferences.getWalletQueueBindingKey(original.token)!!
        assertNotEquals(oldBinding, reboundBinding)
        assertTrue(
            preferences.rememberPendingSms(
                reboundState.filterRevision,
                message,
                reboundBinding
            )
        )
        assertTrue(
            preferences.advanceSmsReconciliationCursor(
                reboundState.filterRevision,
                message
            )
        )

        assertTrue(preferences.completePendingSmsUpload(message, oldBinding))
        assertEquals(
            WalletPairingWriteResult.UPDATED,
            preferences.updateWalletSimBinding(
                original.token,
                WalletSimBinding(30, 0, "SIM 1", "Carrier 30")
            )
        )

        assertEquals(
            SmsInboxReconciliationPolicy.replayCursorBefore(message),
            preferences.getSmsReconciliationState(nowMillis).cursor
        )
    }

    @Test
    fun clearingAllWalletsRotatesTheQueueBindingSecret() {
        val pairing = pairing("TOKENA01", subscriptionId = 10, slotIndex = 0)
        preferences.addWalletPairing(pairing)
        val oldBinding = preferences.getWalletQueueBindingKey(pairing.token)!!

        assertTrue(preferences.clearWalletPairings())
        assertEquals(WalletPairingWriteResult.ADDED, preferences.addWalletPairing(pairing))
        val newBinding = preferences.getWalletQueueBindingKey(pairing.token)!!

        assertNotEquals(oldBinding, newBinding)
        assertNull(preferences.getWalletPairingByQueueBindingKey(oldBinding))
        assertEquals(pairing, preferences.getWalletPairingByQueueBindingKey(newBinding))
    }

    @Test
    fun partialStoredWalletJsonIsSanitizedWithoutCrashing() {
        val encryptedPreferences = encryptedPreferences()
        assertTrue(
            encryptedPreferences.edit()
                .putString(
                    "wallet_pairings",
                    """[{}, {"token":" partial01 ","phoneNumber":null,"label":null,"smsSenderFilters":null}]"""
                )
                .commit()
        )

        val storedWallet = PreferenceManager(context).getWalletPairings().single()

        assertEquals("PARTIAL01", storedWallet.token)
        assertEquals("", storedWallet.phoneNumber)
        assertEquals("", storedWallet.label)
        assertTrue(storedWallet.smsSenderFilters.isEmpty())
        assertTrue(storedWallet.isActive)
    }

    @Test
    fun legacySingleWalletValuesMigrateIntoEncryptedPairingList() {
        val revisionBeforeMigration = preferences
            .getSmsReconciliationState(System.currentTimeMillis())
            .filterRevision
        val legacyPreferences = encryptedPreferences()
        assertTrue(
            legacyPreferences.edit()
                .putString("pairing_token", "legacy01")
                .putString("device_phone", "01012345678")
                .putString("device_label", "Legacy wallet")
                .putString("sms_filters", "[\"VF-Cash\",\"OrangeCash\"]")
                .putFloat("last_balance", 125.5f)
                .commit()
        )

        val migratedPreferences = PreferenceManager(context)
        val migrated = migratedPreferences.getWalletPairings().single()

        assertEquals("LEGACY01", migrated.token)
        assertEquals("01012345678", migrated.phoneNumber)
        assertEquals("Legacy wallet", migrated.label)
        assertEquals(listOf("VF-Cash", "OrangeCash"), migrated.smsSenderFilters)
        assertEquals(125.5, migrated.currentBalance, 0.001)
        assertNull(migrated.subscriptionId)
        assertNull(migrated.simSlotIndex)
        assertFalse(legacyPreferences.contains("pairing_token"))
        assertNotEquals(
            revisionBeforeMigration,
            migratedPreferences.getSmsReconciliationState(System.currentTimeMillis()).filterRevision
        )
    }

    @Test
    fun migrationPreservesAnOlderInboxCheckpointForSafeReplay() {
        val oldCursor = SmsInboxCursor(
            receivedAtMillis = System.currentTimeMillis() -
                (SmsInboxReconciliationPolicy.INITIAL_LOOKBACK_MILLIS * 3),
            messageId = 55L
        )
        val legacyPreferences = encryptedPreferences()
        assertTrue(
            legacyPreferences.edit()
                .putString("pairing_token", "legacy02")
                .putLong("sms_reconciliation_timestamp", oldCursor.receivedAtMillis)
                .putLong("sms_reconciliation_message_id", oldCursor.messageId)
                .commit()
        )
        val migratedPreferences = PreferenceManager(context)

        assertEquals(
            oldCursor,
            migratedPreferences.getSmsReconciliationState(System.currentTimeMillis()).cursor
        )
    }

    private fun pairing(
        token: String,
        subscriptionId: Int,
        slotIndex: Int,
        filters: List<String> = listOf("VF-Cash")
    ): WalletPairing = WalletPairing(
        token = token,
        subscriptionId = subscriptionId,
        simSlotIndex = slotIndex,
        simLabel = "SIM ${slotIndex + 1}",
        carrierLabel = "Carrier $subscriptionId",
        phoneNumber = "0100000000$slotIndex",
        label = "Wallet $token",
        smsSenderFilters = filters
    )

    private fun syncSnapshot(filters: List<String>): WalletSyncSnapshot = WalletSyncSnapshot(
        phoneNumber = "01099999999",
        label = "Synced wallet",
        smsSenderFilters = filters,
        currentBalance = 500.0,
        dailyLimit = 30_000.0,
        monthlyLimit = 100_000.0,
        dailyReceived = 1_000.0,
        monthlyReceived = 5_000.0,
        isActive = true
    )

    private fun encryptedPreferences(): SharedPreferences {
        val masterKeyAlias = MasterKeys.getOrCreate(MasterKeys.AES256_GCM_SPEC)
        return EncryptedSharedPreferences.create(
            "payment_listener_secure_prefs",
            masterKeyAlias,
            context,
            EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
            EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM
        )
    }
}
