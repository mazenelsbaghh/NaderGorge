package com.nadergorge.paymentlistener.service

import android.Manifest
import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.os.Build
import android.util.Log
import androidx.core.app.NotificationCompat
import androidx.core.app.NotificationManagerCompat
import androidx.core.content.ContextCompat
import com.nadergorge.paymentlistener.MainActivity

object WalletRoutingAlertNotifier {
    private const val CHANNEL_ID = "wallet_routing_attention"
    private const val NOTIFICATION_ID = 2102
    private const val TAG = "WalletRoutingAlert"

    fun show(context: Context) {
        if (!canPostNotifications(context)) return
        createChannel(context)
        try {
            NotificationManagerCompat.from(context).notify(
                NOTIFICATION_ID,
                buildNotification(context)
            )
        } catch (error: SecurityException) {
            Log.w(TAG, "Wallet routing notification permission is unavailable.", error)
        }
    }

    private fun buildNotification(context: Context): Notification {
        val openAppIntent = PendingIntent.getActivity(
            context,
            NOTIFICATION_ID,
            Intent(context, MainActivity::class.java),
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT
        )
        return NotificationCompat.Builder(context, CHANNEL_ID)
            .setSmallIcon(android.R.drawable.stat_sys_warning)
            .setContentTitle("تعذر تحديد خط رسالة محفظة")
            .setContentText("افتح Massar PAY وراجع ربط الخطوط. لم تُرسل الرسالة تلقائيًا.")
            .setContentIntent(openAppIntent)
            .setAutoCancel(true)
            .setOnlyAlertOnce(true)
            .setPriority(NotificationCompat.PRIORITY_HIGH)
            .build()
    }

    private fun canPostNotifications(context: Context): Boolean =
        Build.VERSION.SDK_INT < Build.VERSION_CODES.TIRAMISU ||
            ContextCompat.checkSelfPermission(
                context,
                Manifest.permission.POST_NOTIFICATIONS
            ) == PackageManager.PERMISSION_GRANTED

    private fun createChannel(context: Context) {
        val channel = NotificationChannel(
            CHANNEL_ID,
            "تنبيهات ربط خطوط المحافظ",
            NotificationManager.IMPORTANCE_HIGH
        ).apply {
            description = "ينبه عند تعذر تحديد الخط الذي استقبل رسالة المحفظة."
        }
        val manager = context.getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager
        manager.createNotificationChannel(channel)
    }
}
