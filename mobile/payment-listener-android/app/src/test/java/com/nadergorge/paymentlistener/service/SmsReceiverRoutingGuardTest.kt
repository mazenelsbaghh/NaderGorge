package com.nadergorge.paymentlistener.service

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class SmsReceiverRoutingGuardTest {
    @Test
    fun slotOnlyIdentityWithoutActiveSubscriptionMappingFailsClosed() {
        assertNull(
            resolveIncomingSmsSimIdentity(
                broadcastSubscriptionId = null,
                broadcastSimSlotIndex = 0,
                slotsBySubscriptionId = emptyMap()
            )
        )
    }

    @Test
    fun slotOnlyIdentityResolvesThroughActiveSubscription() {
        assertEquals(
            IncomingSmsSimIdentity(subscriptionId = 20, simSlotIndex = 0),
            resolveIncomingSmsSimIdentity(
                broadcastSubscriptionId = null,
                broadcastSimSlotIndex = 0,
                slotsBySubscriptionId = mapOf(20 to 0, 30 to 1)
            )
        )
    }

    @Test
    fun missingIdentityUsesOnlyActiveSubscription() {
        assertEquals(
            IncomingSmsSimIdentity(subscriptionId = 10, simSlotIndex = 1),
            resolveIncomingSmsSimIdentity(
                broadcastSubscriptionId = null,
                broadcastSimSlotIndex = null,
                slotsBySubscriptionId = mapOf(10 to 1)
            )
        )
    }

    @Test
    fun missingIdentityWithTwoActiveSubscriptionsFailsClosed() {
        assertNull(
            resolveIncomingSmsSimIdentity(
                broadcastSubscriptionId = null,
                broadcastSimSlotIndex = null,
                slotsBySubscriptionId = mapOf(10 to 0, 20 to 1)
            )
        )
    }

    @Test
    fun conflictingBroadcastAndActiveSlotFailsClosed() {
        assertNull(
            resolveIncomingSmsSimIdentity(
                broadcastSubscriptionId = 10,
                broadcastSimSlotIndex = 0,
                slotsBySubscriptionId = mapOf(10 to 1)
            )
        )
    }

    @Test
    fun staleBroadcastIdentityCannotClaimSlotOccupiedByReplacementSubscription() {
        assertNull(
            resolveIncomingSmsSimIdentity(
                broadcastSubscriptionId = 10,
                broadcastSimSlotIndex = 0,
                slotsBySubscriptionId = mapOf(20 to 0)
            )
        )
    }
}
