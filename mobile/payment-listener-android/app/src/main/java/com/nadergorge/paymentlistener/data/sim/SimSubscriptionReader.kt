package com.nadergorge.paymentlistener.data.sim

import android.Manifest
import android.annotation.SuppressLint
import android.content.Context
import android.content.pm.PackageManager
import android.telephony.SubscriptionManager
import androidx.core.content.ContextCompat

data class SimSubscription(
    val subscriptionId: Int,
    val slotIndex: Int,
    val displayName: String,
    val carrierName: String
)

class SimSubscriptionReader(private val context: Context) {
    @SuppressLint("MissingPermission")
    fun activeSubscriptions(): List<SimSubscription> {
        if (!canReadSubscriptions()) return emptyList()

        val subscriptionManager = context.getSystemService(SubscriptionManager::class.java)
        return subscriptionManager.activeSubscriptionInfoList
            .orEmpty()
            .asSequence()
            .filter { subscription -> subscription.simSlotIndex >= 0 }
            .sortedBy { subscription -> subscription.simSlotIndex }
            .map { subscription ->
                SimSubscription(
                    subscriptionId = subscription.subscriptionId,
                    slotIndex = subscription.simSlotIndex,
                    displayName = subscription.displayName.toString().trim(),
                    carrierName = subscription.carrierName.toString().trim()
                )
            }
            .toList()
    }

    private fun canReadSubscriptions(): Boolean =
        ContextCompat.checkSelfPermission(
            context,
            Manifest.permission.READ_PHONE_STATE
        ) == PackageManager.PERMISSION_GRANTED
}
