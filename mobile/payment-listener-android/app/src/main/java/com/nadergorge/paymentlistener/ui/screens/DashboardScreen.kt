package com.nadergorge.paymentlistener.ui.screens

import androidx.compose.animation.*
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.nadergorge.paymentlistener.data.api.ApiClient
import com.nadergorge.paymentlistener.data.api.OtherDeviceDto
import com.nadergorge.paymentlistener.data.api.SyncStatusRequest
import com.nadergorge.paymentlistener.data.api.SyncStatusResponse
import com.nadergorge.paymentlistener.data.preference.PreferenceManager
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.text.SimpleDateFormat
import java.util.*

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DashboardScreen(
    prefManager: PreferenceManager,
    onDisconnect: () -> Unit
) {
    var syncData by remember { mutableStateOf<SyncStatusResponse?>(null) }
    var isSyncing by remember { mutableStateOf(false) }
    var errorMsg by remember { mutableStateOf<String?>(null) }
    var lastSyncTime by remember { mutableStateOf<String>("لم تتم المزامنة بعد") }
    val scope = rememberCoroutineScope()
    val context = LocalContext.current

    // Periodical background syncing (heartbeat) every 30 seconds
    LaunchedEffect(Unit) {
        while (true) {
            isSyncing = true
            try {
                val token = prefManager.getPairingToken()
                val apiService = ApiClient.getApiService(context)
                if (token != null && apiService != null) {
                    val balance = prefManager.getLastBalance().toDouble()
                    val response = withContext(Dispatchers.IO) {
                        apiService.syncStatus(token, SyncStatusRequest(balance))
                    }

                    if (response.isSuccessful && response.body()?.success == true) {
                        val data = response.body()!!.data!!
                        syncData = data
                        prefManager.saveSmsFilters(data.smsSenderFilters)
                        prefManager.saveLastBalance(data.currentBalance.toFloat())
                        errorMsg = null
                        lastSyncTime = SimpleDateFormat("hh:mm:ss a", Locale.getDefault()).format(Date())
                    } else {
                        errorMsg = response.body()?.message ?: "فشل المزامنة مع السيرفر."
                    }
                } else {
                    errorMsg = "إعدادات الاتصال غير مكتملة."
                }
            } catch (e: Exception) {
                e.printStackTrace()
                errorMsg = "تعذر الاتصال بالسيرفر. يرجى التحقق من الشبكة."
            } finally {
                isSyncing = false
            }
            delay(30000) // 30 seconds
        }
    }

    val manualSync = {
        scope.launch {
            isSyncing = true
            errorMsg = null
            try {
                val token = prefManager.getPairingToken()
                val apiService = ApiClient.getApiService(context)
                if (token != null && apiService != null) {
                    val balance = prefManager.getLastBalance().toDouble()
                    val response = withContext(Dispatchers.IO) {
                        apiService.syncStatus(token, SyncStatusRequest(balance))
                    }

                    if (response.isSuccessful && response.body()?.success == true) {
                        val data = response.body()!!.data!!
                        syncData = data
                        prefManager.saveSmsFilters(data.smsSenderFilters)
                        prefManager.saveLastBalance(data.currentBalance.toFloat())
                        lastSyncTime = SimpleDateFormat("hh:mm:ss a", Locale.getDefault()).format(Date())
                    } else {
                        errorMsg = response.body()?.message ?: "فشل في تحديث البيانات."
                    }
                } else {
                    errorMsg = "إعدادات الربط مفقودة."
                }
            } catch (e: Exception) {
                e.printStackTrace()
                errorMsg = "فشل الاتصال: ${e.localizedMessage}"
            } finally {
                isSyncing = false
            }
        }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("لوحة التحكم بالمستمع", fontWeight = FontWeight.Bold) },
                actions = {
                    IconButton(onClick = { manualSync() }, enabled = !isSyncing) {
                        Text("مزامنة", color = MaterialTheme.colorScheme.primary, fontWeight = FontWeight.Bold)
                    }
                }
            )
        }
    ) { innerPadding ->
        LazyColumn(
            modifier = Modifier
                .fillMaxSize()
                .padding(innerPadding)
                .padding(horizontal = 16.dp),
            verticalArrangement = Arrangement.spacedBy(16.dp)
        ) {
            // Status Card
            item {
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(16.dp),
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant)
                ) {
                    Column(modifier = Modifier.padding(16.dp)) {
                        Row(
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.SpaceBetween,
                            modifier = Modifier.fillMaxWidth()
                        ) {
                            Column {
                                Text(
                                    text = syncData?.label ?: prefManager.getDeviceLabel() ?: "جهاز غير معروف",
                                    fontSize = 18.sp,
                                    fontWeight = FontWeight.Bold
                                )
                                Text(
                                    text = syncData?.phoneNumber ?: prefManager.getDevicePhone() ?: "بدون رقم",
                                    fontSize = 14.sp,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant
                                )
                            }

                            // Connection dot
                            Row(verticalAlignment = Alignment.CenterVertically) {
                                Box(
                                    modifier = Modifier
                                        .size(10.dp)
                                        .clip(CircleShape)
                                        .background(if (errorMsg == null && syncData != null) Color(0xFF4CAF50) else Color(0xFFF44336))
                                )
                                Spacer(modifier = Modifier.width(6.dp))
                                Text(
                                    text = if (errorMsg == null && syncData != null) "متصل بالسيرفر" else "غير متصل",
                                    fontSize = 12.sp,
                                    fontWeight = FontWeight.Bold,
                                    color = if (errorMsg == null && syncData != null) Color(0xFF4CAF50) else Color(0xFFF44336)
                                )
                            }
                        }

                        Divider(modifier = Modifier.padding(vertical = 12.dp))

                        Row(
                            horizontalArrangement = Arrangement.SpaceBetween,
                            modifier = Modifier.fillMaxWidth()
                        ) {
                            Column {
                                Text("الرصيد الحالي", fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                                Text(
                                    text = "${String.format("%,.2f", syncData?.currentBalance ?: prefManager.getLastBalance().toDouble())} ج.م",
                                    fontSize = 20.sp,
                                    fontWeight = FontWeight.Black,
                                    color = MaterialTheme.colorScheme.primary
                                )
                            }

                            Column(horizontalAlignment = Alignment.End) {
                                Text("آخر مزامنة", fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                                Text(text = lastSyncTime, fontSize = 14.sp, fontWeight = FontWeight.Bold)
                            }
                        }
                    }
                }
            }

            // Error message block
            if (errorMsg != null) {
                item {
                    Card(
                        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.errorContainer),
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Row(modifier = Modifier.padding(16.dp), verticalAlignment = Alignment.CenterVertically) {
                            Text(
                                text = errorMsg!!,
                                color = MaterialTheme.colorScheme.onErrorContainer,
                                fontSize = 14.sp,
                                modifier = Modifier.weight(1f)
                            )
                        }
                    }
                }
            }

            // Wallet limits progress
            item {
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(16.dp)
                ) {
                    Column(modifier = Modifier.padding(16.dp)) {
                        Text(
                            text = "متابعة حدود الاستقبال",
                            fontSize = 16.sp,
                            fontWeight = FontWeight.Bold,
                            modifier = Modifier.padding(bottom = 12.dp)
                        )

                        // Daily Limit
                        val dailyLimit = syncData?.dailyLimit ?: 0.0
                        val dailyReceived = syncData?.dailyReceived ?: 0.0
                        val dailyRatio = if (dailyLimit > 0) (dailyReceived / dailyLimit).toFloat() else 0f
                        Text(
                            text = "اليومي: ${dailyReceived.toInt()} / ${dailyLimit.toInt()} ج.م",
                            fontSize = 13.sp,
                            fontWeight = FontWeight.Medium
                        )
                        LinearProgressIndicator(
                            progress = dailyRatio.coerceIn(0f, 1f),
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(vertical = 6.dp)
                                .height(8.dp)
                                .clip(RoundedCornerShape(4.dp))
                        )

                        Spacer(modifier = Modifier.height(8.dp))

                        // Monthly Limit
                        val monthlyLimit = syncData?.monthlyLimit ?: 0.0
                        val monthlyReceived = syncData?.monthlyReceived ?: 0.0
                        val monthlyRatio = if (monthlyLimit > 0) (monthlyReceived / monthlyLimit).toFloat() else 0f
                        Text(
                            text = "الشهري: ${monthlyReceived.toInt()} / ${monthlyLimit.toInt()} ج.م",
                            fontSize = 13.sp,
                            fontWeight = FontWeight.Medium
                        )
                        LinearProgressIndicator(
                            progress = monthlyRatio.coerceIn(0f, 1f),
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(vertical = 6.dp)
                                .height(8.dp)
                                .clip(RoundedCornerShape(4.dp))
                        )
                    }
                }
            }

            // SMS filters information
            item {
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    shape = RoundedCornerShape(16.dp)
                ) {
                    Column(modifier = Modifier.padding(16.dp)) {
                        Text(
                            text = "جهات إرسال الرسائل النشطة",
                            fontSize = 16.sp,
                            fontWeight = FontWeight.Bold,
                            modifier = Modifier.padding(bottom = 6.dp)
                        )
                        Text(
                            text = "يقوم التطبيق بالتقاط رسائل SMS الصادرة عن الجهات التالية فقط ومزامنتها تلقائياً مع السيرفر:",
                            fontSize = 12.sp,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                            modifier = Modifier.padding(bottom = 12.dp)
                        )
                        
                        val filters = syncData?.smsSenderFilters ?: prefManager.getSmsFilters()
                        Text(
                            text = filters.joinToString(", "),
                            fontFamily = FontFamily.Monospace,
                            fontWeight = FontWeight.Bold,
                            color = MaterialTheme.colorScheme.primary,
                            fontSize = 14.sp
                        )
                    }
                }
            }

            // Other Devices Section
            val otherDevices = syncData?.otherDevices ?: emptyList()
            if (otherDevices.isNotEmpty()) {
                item {
                    Text(
                        text = "حالة أجهزة المحافظ الأخرى",
                        fontSize = 16.sp,
                        fontWeight = FontWeight.Bold,
                        modifier = Modifier.padding(top = 8.dp)
                    )
                }

                items(otherDevices) { device ->
                    Card(
                        modifier = Modifier.fillMaxWidth(),
                        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
                    ) {
                        Row(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(12.dp),
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.SpaceBetween
                        ) {
                            Column {
                                Text(text = device.label, fontWeight = FontWeight.Bold, fontSize = 14.sp)
                                Text(text = device.phoneNumber, fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                            }
                            Row(verticalAlignment = Alignment.CenterVertically) {
                                Text(
                                    text = "${String.format("%,.0f", device.currentBalance)} ج.م",
                                    fontWeight = FontWeight.Bold,
                                    fontSize = 14.sp,
                                    modifier = Modifier.padding(end = 12.dp)
                                )
                                Box(
                                    modifier = Modifier
                                        .size(8.dp)
                                        .clip(CircleShape)
                                        .background(if (device.deviceStatus == "Connected") Color(0xFF4CAF50) else Color(0xFF9E9E9E))
                                )
                            }
                        }
                    }
                }
            }

            // Disconnect/Exit action button
            item {
                Button(
                    onClick = {
                        prefManager.clearConfiguration()
                        onDisconnect()
                    },
                    colors = ButtonDefaults.buttonColors(containerColor = MaterialTheme.colorScheme.error),
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(top = 16.dp, bottom = 32.dp)
                        .height(50.dp)
                ) {
                    Text("قطع اتصال المحفظة وإعادة الضبط", fontWeight = FontWeight.Bold)
                }
            }
        }
    }
}
