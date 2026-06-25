package com.nadergorge.paymentlistener

import android.Manifest
import android.content.pm.PackageManager
import android.os.Bundle
import android.widget.Toast
import androidx.activity.ComponentActivity
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.compose.setContent
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.core.content.ContextCompat
import com.nadergorge.paymentlistener.data.preference.PreferenceManager
import com.nadergorge.paymentlistener.service.BackgroundSyncScheduler
import com.nadergorge.paymentlistener.ui.screens.DashboardScreen
import com.nadergorge.paymentlistener.ui.screens.SetupScreen

class MainActivity : ComponentActivity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        
        val prefManager = PreferenceManager(this)
        if (!prefManager.getServerUrl().isNullOrBlank() && !prefManager.getPairingToken().isNullOrBlank()) {
            BackgroundSyncScheduler.schedule(this)
        }

        setContent {
            MaterialTheme {
                Surface(
                    modifier = Modifier.fillMaxSize(),
                    color = MaterialTheme.colorScheme.background
                ) {
                    var currentScreen by remember {
                        mutableStateOf(
                            if (prefManager.getServerUrl() != null && prefManager.getPairingToken() != null) {
                                "dashboard"
                            } else {
                                "setup"
                            }
                        )
                    }

                    var hasSmsPermission by remember {
                        mutableStateOf(
                            ContextCompat.checkSelfPermission(
                                this,
                                Manifest.permission.RECEIVE_SMS
                            ) == PackageManager.PERMISSION_GRANTED &&
                            ContextCompat.checkSelfPermission(
                                this,
                                Manifest.permission.READ_SMS
                            ) == PackageManager.PERMISSION_GRANTED
                        )
                    }

                    val permissionLauncher = rememberLauncherForActivityResult(
                        contract = ActivityResultContracts.RequestMultiplePermissions()
                    ) { permissions ->
                        val receiveGranted = permissions[Manifest.permission.RECEIVE_SMS] ?: false
                        val readGranted = permissions[Manifest.permission.READ_SMS] ?: false
                        
                        hasSmsPermission = receiveGranted && readGranted
                        if (!hasSmsPermission) {
                            Toast.makeText(
                                this,
                                "التطبيق بحاجة لصلاحيات قراءة الـ SMS لالتقاط رسائل التحويل الرقمي.",
                                Toast.LENGTH_LONG
                            ).show()
                        }
                    }

                    LaunchedEffect(currentScreen) {
                        if (currentScreen == "dashboard" && !hasSmsPermission) {
                            permissionLauncher.launch(
                                arrayOf(
                                    Manifest.permission.RECEIVE_SMS,
                                    Manifest.permission.READ_SMS
                                )
                            )
                        }
                    }

                    when (currentScreen) {
                        "setup" -> {
                            SetupScreen(
                                prefManager = prefManager,
                                onSetupSuccess = {
                                    BackgroundSyncScheduler.schedule(this)
                                    currentScreen = "dashboard"
                                }
                            )
                        }
                        "dashboard" -> {
                            if (hasSmsPermission) {
                                DashboardScreen(
                                    prefManager = prefManager,
                                    onDisconnect = {
                                        BackgroundSyncScheduler.cancel(this)
                                        currentScreen = "setup"
                                    }
                                )
                            } else {
                                PermissionRequiredScreen(
                                    onRequestPermission = {
                                        permissionLauncher.launch(
                                            arrayOf(
                                                Manifest.permission.RECEIVE_SMS,
                                                Manifest.permission.READ_SMS
                                            )
                                        )
                                    }
                                )
                            }
                        }
                    }
                }
            }
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
            text = "صلاحيات الـ SMS مطلوبة",
            fontSize = 22.sp,
            fontWeight = FontWeight.Bold,
            color = MaterialTheme.colorScheme.error,
            modifier = Modifier.padding(bottom = 8.dp)
        )
        
        Text(
            text = "لكي يتمكن التطبيق من التقاط رسائل المحافظ الرقمية وإرسالها تلقائياً للسيرفر لتفعيل رصيد الطلاب، فإنه يحتاج إلى صلاحية استقبال وقراءة الرسائل النصية القصيرة (SMS).",
            fontSize = 14.sp,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            textAlign = TextAlign.Center,
            modifier = Modifier.padding(bottom = 24.dp)
        )

        Button(
            onClick = onRequestPermission,
            modifier = Modifier.fillMaxWidth().height(50.dp)
        ) {
            Text("منح الصلاحيات المطلوبة", fontWeight = FontWeight.Bold)
        }
    }
}
