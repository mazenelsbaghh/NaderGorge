package com.nadergorge.paymentlistener.data.preference

import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import com.nadergorge.paymentlistener.service.SmsInboxCursor
import com.nadergorge.paymentlistener.service.SmsInboxReconciliationPolicy
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class PreferenceManagerReconciliationTest {
    private lateinit var preferences: PreferenceManager

    @Before
    fun resetPreferences() {
        val context = InstrumentationRegistry.getInstrumentation().context
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
        preferences.saveSmsFilters(listOf("VF-Cash"))
        val initialState = preferences.getSmsReconciliationState(nowMillis)
        val uploadedCursor = SmsInboxCursor(nowMillis - 1_000L, 7L)
        assertTrue(
            preferences.advanceSmsReconciliationCursor(
                initialState.filterRevision,
                uploadedCursor
            )
        )

        preferences.saveSmsFilters(listOf("OrangeCash"))
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
        preferences.saveSmsFilters(listOf("VF-Cash", "OrangeCash"))
        val state = preferences.getSmsReconciliationState(nowMillis)
        val uploadedCursor = SmsInboxCursor(nowMillis - 1_000L, 20L)
        assertTrue(preferences.advanceSmsReconciliationCursor(state.filterRevision, uploadedCursor))

        preferences.saveSmsFilters(listOf(" orangecash ", "vf-cash"))
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
}
