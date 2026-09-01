package com.nadergorge.paymentlistener

import android.Manifest
import android.content.ActivityNotFoundException
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.os.PowerManager
import android.provider.Settings
import android.util.Log
import android.widget.Toast
import androidx.activity.ComponentActivity
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.compose.setContent
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalLayoutDirection
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.LayoutDirection
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.core.content.ContextCompat
import com.nadergorge.paymentlistener.data.preference.PreferenceManager
import com.nadergorge.paymentlistener.service.BackgroundSyncScheduler
import com.nadergorge.paymentlistener.ui.screens.DashboardScreen
import com.nadergorge.paymentlistener.ui.screens.SetupScreen

private enum class AppScreen {
    SETUP,
    DASHBOARD
}

private const val ACTIVITY_LOG_TAG = "MainActivity"

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        val preferences = PreferenceManager(this)
        if (preferences.hasWalletPairings()) startBackgroundServices()

        setContent {
            CompositionLocalProvider(LocalLayoutDirection provides LayoutDirection.Rtl) {
                MaterialTheme {
                    Surface(
                        modifier = Modifier.fillMaxSize(),
                        color = MaterialTheme.colorScheme.background
                    ) {
                        PaymentListenerApp(preferences)
                    }
                }
            }
        }
    }

    @Composable
    private fun PaymentListenerApp(preferences: PreferenceManager) {
        var appScreen by remember {
            mutableStateOf(
                if (preferences.hasWalletPairings()) AppScreen.DASHBOARD else AppScreen.SETUP
            )
        }
        var permissionsGranted by remember { mutableStateOf(requiredPermissionsGranted()) }
        var permissionRequestStarted by remember { mutableStateOf(false) }

        val permissionLauncher = rememberLauncherForActivityResult(
            contract = ActivityResultContracts.RequestMultiplePermissions()
        ) {
            permissionsGranted = requiredPermissionsGranted()
            if (permissionsGranted && preferences.hasWalletPairings()) {
                startBackgroundServices()
            } else if (!permissionsGranted) {
                Toast.makeText(
                    this,
                    "يلزم السماح بقراءة الرسائل وحالة الشرائح لربط كل محفظة بخطها الصحيح.",
                    Toast.LENGTH_LONG
                ).show()
            }
        }

        LaunchedEffect(Unit) {
            if (!permissionsGranted && !permissionRequestStarted) {
                permissionRequestStarted = true
                permissionLauncher.launch(runtimePermissions())
            }
        }

        if (!permissionsGranted) {
            PermissionRequiredScreen {
                permissionLauncher.launch(runtimePermissions())
            }
            return
        }

        when (appScreen) {
            AppScreen.SETUP -> SetupScreen(
                prefManager = preferences,
                onSetupSuccess = {
                    startBackgroundServices()
                    appScreen = AppScreen.DASHBOARD
                },
                onCancel = if (preferences.hasWalletPairings()) {
                    { appScreen = AppScreen.DASHBOARD }
                } else {
                    null
                }
            )

            AppScreen.DASHBOARD -> DashboardScreen(
                prefManager = preferences,
                onAddWallet = { appScreen = AppScreen.SETUP },
                onAllWalletsRemoved = {
                    BackgroundSyncScheduler.cancel(this)
                    appScreen = AppScreen.SETUP
                }
            )
        }
    }

    private fun runtimePermissions(): Array<String> {
        val permissions = mutableListOf(
            Manifest.permission.RECEIVE_SMS,
            Manifest.permission.READ_SMS,
            Manifest.permission.READ_PHONE_STATE
        )

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            permissions += Manifest.permission.POST_NOTIFICATIONS
        }

        return permissions.toTypedArray()
    }

    private fun requiredPermissionsGranted(): Boolean = listOf(
        Manifest.permission.RECEIVE_SMS,
        Manifest.permission.READ_SMS,
        Manifest.permission.READ_PHONE_STATE
    ).all { permission ->
        ContextCompat.checkSelfPermission(this, permission) == PackageManager.PERMISSION_GRANTED
    }

    private fun startBackgroundServices() {
        BackgroundSyncScheduler.schedule(this)
        BackgroundSyncScheduler.startRealtimeService(this)
        requestBatteryOptimizationBypassIfNeeded()
    }

    private fun requestBatteryOptimizationBypassIfNeeded() {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.M) return

        val powerManager = getSystemService(Context.POWER_SERVICE) as PowerManager
        if (powerManager.isIgnoringBatteryOptimizations(packageName)) return

        try {
            startActivity(
                Intent(Settings.ACTION_REQUEST_IGNORE_BATTERY_OPTIMIZATIONS).apply {
                    data = Uri.parse("package:$packageName")
                }
            )
        } catch (error: ActivityNotFoundException) {
            Log.w(ACTIVITY_LOG_TAG, "Battery optimization settings are unavailable", error)
        } catch (error: SecurityException) {
            Log.w(ACTIVITY_LOG_TAG, "Battery optimization request was denied", error)
        }
    }
}

@Composable
fun PermissionRequiredScreen(onRequestPermission: () -> Unit) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(24.dp),
        verticalArrangement = Arrangement.Center,
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Text(
            text = "صلاحيات الرسائل والشرائح مطلوبة",
            fontSize = 22.sp,
            fontWeight = FontWeight.Bold,
            color = MaterialTheme.colorScheme.error,
            textAlign = TextAlign.Center,
            modifier = Modifier.padding(bottom = 8.dp)
        )

        Text(
            text = "يستخدم Massar PAY حالة الشرائح لتوجيه رسالة كل خط إلى محفظته، ويقرأ رسائل المحافظ فقط لإرسال التحصيلات إلى المنصة.",
            fontSize = 14.sp,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            textAlign = TextAlign.Center,
            modifier = Modifier.padding(bottom = 24.dp)
        )

        Button(
            onClick = onRequestPermission,
            modifier = Modifier
                .fillMaxWidth()
                .heightIn(min = 52.dp)
        ) {
            Text("منح الصلاحيات المطلوبة", fontWeight = FontWeight.Bold)
        }
    }
}
