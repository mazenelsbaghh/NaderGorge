package com.nadergorge.paymentlistener.service

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import com.nadergorge.paymentlistener.data.preference.PreferenceManager

class BootReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        if (intent.action != Intent.ACTION_BOOT_COMPLETED) return

        val prefManager = PreferenceManager(context)
        if (!prefManager.getServerUrl().isNullOrBlank() && !prefManager.getPairingToken().isNullOrBlank()) {
            BackgroundSyncScheduler.schedule(context)
            BackgroundSyncScheduler.startRealtimeService(context)
        }
    }
}
