package com.nadergorge.parent.service

import com.google.firebase.messaging.FirebaseMessagingService
import com.google.firebase.messaging.RemoteMessage

class ParentFirebaseMessagingService : FirebaseMessagingService() {

    override fun onNewToken(token: String) {
        super.onNewToken(token)
    }

    override fun onMessageReceived(remoteMessage: RemoteMessage) {
        super.onMessageReceived(remoteMessage)

        val title = remoteMessage.notification?.title ?: remoteMessage.data["title"] ?: "تنبيه جديد"
        val body = remoteMessage.notification?.body ?: remoteMessage.data["body"] ?: ""
        val studentId = remoteMessage.data["studentId"]

        ParentRefreshBus.notifyStudentChanged(studentId)
        sendNotification(title, body)
    }

    private fun sendNotification(title: String, messageBody: String) {
        NotificationHelper.showLocalNotification(this, title, messageBody)
    }
}
