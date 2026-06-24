package com.nadergorge.parent

import android.app.Application
import com.google.firebase.FirebaseApp

class ParentApplication : Application() {
    override fun onCreate() {
        super.onCreate()
        try {
            FirebaseApp.initializeApp(this)
        } catch (e: Exception) {
            e.printStackTrace()
        }
    }
}
