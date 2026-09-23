package com.massar.parent

import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Intent
import android.os.Handler
import android.os.Looper
import androidx.core.app.NotificationCompat
import com.google.firebase.messaging.FirebaseMessagingService
import com.google.firebase.messaging.RemoteMessage
import io.flutter.plugin.common.MethodChannel

class ParentMessagingService : FirebaseMessagingService() {
    companion object { var channel: MethodChannel? = null }
    override fun onNewToken(token: String) {
        Handler(Looper.getMainLooper()).post { channel?.invokeMethod("tokenChanged", null) }
    }
    override fun onMessageReceived(message: RemoteMessage) {
        Handler(Looper.getMainLooper()).post { channel?.invokeMethod("refresh", null) }
        val manager = getSystemService(NotificationManager::class.java)
        if (!manager.areNotificationsEnabled()) return
        manager.createNotificationChannel(NotificationChannel("parent_updates", "متابعة الطالب", NotificationManager.IMPORTANCE_DEFAULT))
        val intent = Intent(this, MainActivity::class.java).addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP or Intent.FLAG_ACTIVITY_SINGLE_TOP)
        val pending = PendingIntent.getActivity(this, 0, intent, PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE)
        val notification = NotificationCompat.Builder(this, "parent_updates")
            .setSmallIcon(R.drawable.ic_notification)
            .setContentTitle(message.notification?.title ?: message.data["title"] ?: "مسار ولي الأمر")
            .setContentText(message.notification?.body ?: message.data["body"] ?: "توجد مستجدات في متابعة الطالب")
            .setContentIntent(pending).setAutoCancel(true).build()
        manager.notify(message.messageId?.hashCode() ?: System.currentTimeMillis().toInt(), notification)
    }
}
