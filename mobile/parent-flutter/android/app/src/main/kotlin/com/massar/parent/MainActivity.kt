package com.massar.parent

import android.Manifest
import android.content.Intent
import android.content.pm.PackageManager
import android.os.Build
import android.provider.Settings
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKeys
import com.google.firebase.messaging.FirebaseMessaging
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel
import java.security.GeneralSecurityException
import java.io.IOException

class MainActivity : FlutterActivity() {
    private var permissionResult: MethodChannel.Result? = null
    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)
        val channel = MethodChannel(flutterEngine.dartExecutor.binaryMessenger, "net.massaracademy.parent/device")
        ParentMessagingService.channel = channel
        channel.setMethodCallHandler { call, result ->
            when (call.method) {
                "legacyProfiles" -> readLegacy(result)
                "deviceToken" -> FirebaseMessaging.getInstance().token.addOnCompleteListener { task ->
                    if (task.isSuccessful) result.success(task.result)
                    else result.error("push", "Notification registration unavailable", null)
                }
                "requestNotifications" -> {
                    if (Build.VERSION.SDK_INT < 33 || checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS) == PackageManager.PERMISSION_GRANTED) result.success(true)
                    else if (permissionResult != null) result.error("busy", "Permission request active", null)
                    else { permissionResult = result; requestPermissions(arrayOf(Manifest.permission.POST_NOTIFICATIONS), 100) }
                }
                "openSettings" -> { startActivity(Intent(Settings.ACTION_APP_NOTIFICATION_SETTINGS).putExtra(Settings.EXTRA_APP_PACKAGE, packageName)); result.success(null) }
                else -> result.notImplemented()
            }
        }
    }
    private fun readLegacy(result: MethodChannel.Result) {
        try {
            if (!java.io.File(applicationInfo.dataDir, "shared_prefs/parent_secure_prefs.xml").exists()) {
                result.success(emptyMap<String, String>()); return
            }
            val prefs = EncryptedSharedPreferences.create("parent_secure_prefs", MasterKeys.getOrCreate(MasterKeys.AES256_GCM_SPEC), this,
                EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV, EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM)
            result.success(mapOf("profiles" to prefs.getString("linked_students", null), "activeId" to prefs.getString("active_student_id", null)))
        } catch (error: GeneralSecurityException) { result.error("migration", "Unable to unlock legacy profiles", null) }
          catch (error: IOException) { result.error("migration", "Unable to read legacy profiles", null) }
    }
    override fun onRequestPermissionsResult(requestCode: Int, permissions: Array<out String>, grantResults: IntArray) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults)
        if (requestCode == 100) { permissionResult?.success(grantResults.firstOrNull() == PackageManager.PERMISSION_GRANTED); permissionResult = null }
    }
    override fun onNewIntent(intent: Intent) { super.onNewIntent(intent); ParentMessagingService.channel?.invokeMethod("refresh", null) }
    override fun onDestroy() { ParentMessagingService.channel = null; super.onDestroy() }
}
