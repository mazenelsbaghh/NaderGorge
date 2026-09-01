package com.nadergorge.paymentlistener.service

import androidx.work.Data
import com.nadergorge.paymentlistener.data.preference.WalletPairing
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class SmsSyncPayloadTest {
    @Test
    fun receivePayloadPinsWalletBindingAndSimIdentity() {
        val data = SmsSyncWorker.inputData(
            SmsSyncQueuePayload(
                sender = "VF-Cash",
                body = "received 100 EGP",
                receivedAt = "2026-08-31T08:00:00Z",
                walletBindingKey = "opaque-binding-b",
                subscriptionId = 22,
                simSlotIndex = 1
            )
        )

        assertEquals(
            SmsSyncQueuePayload(
                sender = "VF-Cash",
                body = "received 100 EGP",
                receivedAt = "2026-08-31T08:00:00Z",
                walletBindingKey = "opaque-binding-b",
                subscriptionId = 22,
                simSlotIndex = 1
            ),
            SmsSyncWorker.parseInputData(data)
        )
        assertFalse(data.keyValueMap.containsKey("pairing_token"))
    }

    @Test
    fun inboxPayloadPreservesCursorAndRoutesByHistoricalSubscription() {
        val payload = SmsSyncQueuePayload(
            sender = "VF-Cash",
            body = "received 150 EGP",
            receivedAt = "2026-08-31T08:00:00Z",
            walletBindingKey = "opaque-binding-b",
            subscriptionId = 22,
            simSlotIndex = null,
            inboxCursor = SmsInboxCursor(receivedAtMillis = 1_777_777L, messageId = 42L)
        )
        val parsedPayload = SmsSyncWorker.parseInputData(SmsSyncWorker.inputData(payload))
        val pairedWallet = WalletPairing(
            token = "TOKEN-B",
            subscriptionId = 22,
            simSlotIndex = 1,
            smsSenderFilters = listOf("VF-Cash")
        )

        assertEquals(payload, parsedPayload)
        assertEquals(
            "TOKEN-B",
            resolvePinnedSmsUploadRequest(requireNotNull(parsedPayload)) { pairedWallet }
                ?.pairingToken
        )
    }

    @Test
    fun secondWalletBindingResolvesOnlyToSecondWalletToken() {
        val payload = SmsSyncQueuePayload(
            sender = "VF-Cash",
            body = "received 200 EGP",
            receivedAt = "2026-08-31T08:00:00Z",
            walletBindingKey = "opaque-binding-b",
            subscriptionId = 22,
            simSlotIndex = 1
        )
        val bindingTokens = mapOf(
            "opaque-binding-a" to WalletPairing(
                token = "TOKEN-A",
                subscriptionId = 11,
                simSlotIndex = 0,
                smsSenderFilters = listOf("VF-Cash")
            ),
            "opaque-binding-b" to WalletPairing(
                token = "TOKEN-B",
                subscriptionId = 22,
                simSlotIndex = 1,
                smsSenderFilters = listOf("VF-Cash")
            )
        )

        val request = resolvePinnedSmsUploadRequest(payload, bindingTokens::get)

        assertEquals("TOKEN-B", request?.pairingToken)
        assertEquals(payload.body, request?.body)
    }

    @Test
    fun queuedWalletBindingIsRejectedAfterItsSimIdentityChanges() {
        val payload = SmsSyncQueuePayload(
            sender = "VF-Cash",
            body = "received 200 EGP",
            receivedAt = "2026-08-31T08:00:00Z",
            walletBindingKey = "opaque-binding-a",
            subscriptionId = 10,
            simSlotIndex = 0
        )
        val reboundWallet = WalletPairing(
            token = "TOKEN-A",
            subscriptionId = 20,
            simSlotIndex = 1,
            smsSenderFilters = listOf("VF-Cash")
        )

        assertNull(resolvePinnedSmsUploadRequest(payload) { reboundWallet })
    }

    @Test
    fun inactiveWalletUploadIsRetriedWithoutBlockingOtherWalletJobs() {
        assertTrue(shouldRetryQueuedSmsUpload(SmsUploadOutcome.ConfigurationFailure))
        assertFalse(shouldRetryQueuedSmsUpload(SmsUploadOutcome.Success))
    }

    @Test
    fun queuedLegacyPayloadWithoutPinnedWalletFailsClosed() {
        val legacyData = Data.Builder()
            .putString(SmsSyncWorker.INPUT_SENDER, "VF-Cash")
            .putString(SmsSyncWorker.INPUT_BODY, "received 100 EGP")
            .putString(SmsSyncWorker.INPUT_RECEIVED_AT, "2026-06-01T08:00:00Z")
            .build()

        assertNull(SmsSyncWorker.parseInputData(legacyData))
    }

    @Test
    fun payloadPreservesUnknownSimIdentityWithoutDroppingTraceKeys() {
        val data = SmsSyncWorker.inputData(
            SmsSyncQueuePayload(
                sender = "VF-Cash",
                body = "received 100 EGP",
                receivedAt = "2026-08-31T08:00:00Z",
                walletBindingKey = "opaque-binding-a",
                subscriptionId = null,
                simSlotIndex = null
            )
        )

        val payload = SmsSyncWorker.parseInputData(data)

        assertTrue(data.keyValueMap.containsKey(SmsSyncWorker.INPUT_SUBSCRIPTION_ID))
        assertTrue(data.keyValueMap.containsKey(SmsSyncWorker.INPUT_SIM_SLOT_INDEX))
        assertNull(payload?.subscriptionId)
        assertNull(payload?.simSlotIndex)
    }

    @Test
    fun legacyRawTokenPayloadIsRejectedEvenWhenTraceFieldsExist() {
        val legacyData = Data.Builder()
            .putString(SmsSyncWorker.INPUT_SENDER, "VF-Cash")
            .putString(SmsSyncWorker.INPUT_BODY, "received 100 EGP")
            .putString(SmsSyncWorker.INPUT_RECEIVED_AT, "2026-08-31T08:00:00Z")
            .putString("pairing_token", "TOKEN-A")
            .putInt(SmsSyncWorker.INPUT_SUBSCRIPTION_ID, 10)
            .putInt(SmsSyncWorker.INPUT_SIM_SLOT_INDEX, 0)
            .build()

        assertNull(SmsSyncWorker.parseInputData(legacyData))
    }
}
