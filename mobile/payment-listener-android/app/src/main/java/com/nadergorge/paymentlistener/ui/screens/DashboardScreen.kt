package com.nadergorge.paymentlistener.ui.screens

import androidx.compose.animation.AnimatedContent
import androidx.compose.animation.ExperimentalAnimationApi
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.togetherWith
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.AccountCircle
import androidx.compose.material.icons.filled.CreditCard
import androidx.compose.material.icons.filled.Home
import androidx.compose.material.icons.filled.Payments
import androidx.compose.material.icons.filled.PhoneAndroid
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.SignalCellularAlt
import androidx.compose.material.icons.filled.Sms
import androidx.compose.material.icons.filled.TrendingUp
import androidx.compose.material3.AssistChip
import androidx.compose.material3.AssistChipDefaults
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Divider
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
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
import kotlinx.coroutines.withContext
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

private val Navy = Color(0xFF0A1D3D)
private val Teal = Color(0xFF0E8F8F)
private val Gold = Color(0xFFD4A017)
private val OffWhite = Color(0xFFF6F7F8)
private val SoftGray = Color(0xFFEEF1F4)
private val LineGray = Color(0xFFDCE1E6)
private val DarkGray = Color(0xFF2E3A47)
private val Success = Color(0xFF16A06A)
private val Danger = Color(0xFFD94A4A)

private data class DashboardTab(
    val title: String,
    val icon: ImageVector
)

private val dashboardTabs = listOf(
    DashboardTab("الرئيسية", Icons.Filled.Home),
    DashboardTab("المحافظ", Icons.Filled.CreditCard),
    DashboardTab("التحصيلات", Icons.Filled.Payments),
    DashboardTab("الحساب", Icons.Filled.AccountCircle)
)

@OptIn(ExperimentalMaterial3Api::class, ExperimentalAnimationApi::class)
@Composable
fun DashboardScreen(
    prefManager: PreferenceManager,
    onDisconnect: () -> Unit
) {
    var syncData by remember { mutableStateOf<SyncStatusResponse?>(null) }
    var isSyncing by remember { mutableStateOf(false) }
    var errorMsg by remember { mutableStateOf<String?>(null) }
    var lastSyncTime by remember { mutableStateOf("لم تتم المزامنة بعد") }
    var selectedTab by remember { mutableIntStateOf(0) }
    val context = LocalContext.current

    LaunchedEffect(Unit) {
        while (true) {
            isSyncing = true
            try {
                val token = prefManager.getPairingToken()
                val apiService = ApiClient.getApiService(context)
                if (token != null && apiService != null) {
                    val response = withContext(Dispatchers.IO) {
                        apiService.syncStatus(token, SyncStatusRequest(null))
                    }

                    if (response.isSuccessful && response.body()?.success == true) {
                        val data = response.body()!!.data!!
                        syncData = data
                        prefManager.saveSmsFilters(data.smsSenderFilters)
                        prefManager.saveLastBalance(data.currentBalance.toFloat())
                        prefManager.saveDevicePhone(data.phoneNumber)
                        prefManager.saveDeviceLabel(data.label)
                        errorMsg = null
                        lastSyncTime = SimpleDateFormat("hh:mm:ss a", Locale.getDefault()).format(Date())
                    } else {
                        errorMsg = response.body()?.message ?: "فشل الاتصال بالسيرفر."
                    }
                } else {
                    errorMsg = "إعدادات الاتصال غير مكتملة."
                }
            } catch (e: Exception) {
                e.printStackTrace()
                errorMsg = "تعذر الاتصال بالسيرفر. تحقق من الشبكة أو صلاحيات الخلفية."
            } finally {
                isSyncing = false
            }
            delay(30_000)
        }
    }

    Scaffold(
        containerColor = OffWhite,
        topBar = {
            TopAppBar(
                title = {
                    Column(horizontalAlignment = Alignment.End, modifier = Modifier.fillMaxWidth()) {
                        Text("مستمع المدفوعات", color = Navy, fontWeight = FontWeight.Black, fontSize = 20.sp)
                        Text("ربط المحافظ ورسائل SMS", color = DarkGray, fontSize = 12.sp)
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(containerColor = OffWhite),
                actions = {
                    SyncPill(isSyncing = isSyncing, isConnected = errorMsg == null && syncData != null)
                }
            )
        },
        bottomBar = {
            NavigationBar(containerColor = Color.White, tonalElevation = 0.dp) {
                dashboardTabs.forEachIndexed { index, tab ->
                    NavigationBarItem(
                        selected = selectedTab == index,
                        onClick = { selectedTab = index },
                        icon = { Icon(tab.icon, contentDescription = tab.title) },
                        label = { Text(tab.title, fontSize = 11.sp, fontWeight = FontWeight.Bold) },
                        alwaysShowLabel = true
                    )
                }
            }
        }
    ) { innerPadding ->
        AnimatedContent(
            targetState = selectedTab,
            transitionSpec = { fadeIn() togetherWith fadeOut() },
            label = "dashboard-tab",
            modifier = Modifier.padding(innerPadding)
        ) { tab ->
            when (tab) {
                0 -> HomeTab(syncData, errorMsg, lastSyncTime, isSyncing, prefManager)
                1 -> WalletsTab(syncData, prefManager)
                2 -> CollectionsTab(syncData, prefManager, lastSyncTime)
                else -> AccountTab(syncData, prefManager, errorMsg, lastSyncTime, onDisconnect)
            }
        }
    }
}

@Composable
private fun HomeTab(
    syncData: SyncStatusResponse?,
    errorMsg: String?,
    lastSyncTime: String,
    isSyncing: Boolean,
    prefManager: PreferenceManager
) {
    ScreenList {
        item {
            AuthorityCard(
                title = syncData?.label ?: prefManager.getDeviceLabel() ?: "محفظة غير معروفة",
                subtitle = syncData?.phoneNumber ?: prefManager.getDevicePhone() ?: "لم يتم تحميل الرقم بعد",
                amount = syncData?.currentBalance ?: prefManager.getLastBalance().toDouble(),
                isConnected = errorMsg == null && syncData != null
            )
        }

        item {
            if (errorMsg != null) {
                NoticeCard(title = "الاتصال يحتاج متابعة", body = errorMsg, color = Danger)
            } else {
                NoticeCard(
                    title = if (isSyncing) "يتم تحديث الاتصال" else "المزامنة تعمل تلقائياً",
                    body = "آخر تحديث: $lastSyncTime. التطبيق يعمل في الخلفية ويستقبل رسائل المحافظ المسموح بها.",
                    color = Teal
                )
            }
        }

        item {
            Row(horizontalArrangement = Arrangement.spacedBy(10.dp), modifier = Modifier.fillMaxWidth()) {
                MetricTile("تحصيلات اليوم", money(syncData?.dailyReceived ?: 0.0), Icons.Filled.TrendingUp, modifier = Modifier.weight(1f))
                MetricTile("الشهر الحالي", money(syncData?.monthlyReceived ?: 0.0), Icons.Filled.Payments, modifier = Modifier.weight(1f))
            }
        }

        item {
            FiltersCard(syncData?.smsSenderFilters ?: prefManager.getSmsFilters())
        }
    }
}

@Composable
private fun WalletsTab(syncData: SyncStatusResponse?, prefManager: PreferenceManager) {
    val activeWallet = OtherDeviceDto(
        phoneNumber = syncData?.phoneNumber ?: prefManager.getDevicePhone() ?: "بدون رقم",
        label = syncData?.label ?: prefManager.getDeviceLabel() ?: "هذه المحفظة",
        currentBalance = syncData?.currentBalance ?: prefManager.getLastBalance().toDouble(),
        deviceStatus = if (syncData == null) "Disconnected" else "Connected",
        lastSeenAt = null
    )
    val devices = listOf(activeWallet) + (syncData?.otherDevices ?: emptyList())

    ScreenList {
        item { SectionTitle("قائمة المحافظ", "تابع اتصال كل جهاز ورصيد آخر رسالة مؤكدة.") }
        items(devices) { wallet ->
            WalletRow(wallet = wallet, isPrimary = wallet.phoneNumber == activeWallet.phoneNumber)
        }
    }
}

@Composable
private fun CollectionsTab(syncData: SyncStatusResponse?, prefManager: PreferenceManager, lastSyncTime: String) {
    ScreenList {
        item {
            SectionTitle("التحصيلات", "ملخص الاستقبال وحدود اليوم والشهر من السيرفر.")
        }
        item {
            LimitCard(
                title = "الحد اليومي",
                received = syncData?.dailyReceived ?: 0.0,
                limit = syncData?.dailyLimit ?: 0.0
            )
        }
        item {
            LimitCard(
                title = "الحد الشهري",
                received = syncData?.monthlyReceived ?: 0.0,
                limit = syncData?.monthlyLimit ?: 0.0
            )
        }
        item {
            TransactionSummaryCard(
                walletLabel = syncData?.label ?: prefManager.getDeviceLabel() ?: "المحفظة الحالية",
                lastSyncTime = lastSyncTime,
                balance = syncData?.currentBalance ?: prefManager.getLastBalance().toDouble()
            )
        }
    }
}

@Composable
private fun AccountTab(
    syncData: SyncStatusResponse?,
    prefManager: PreferenceManager,
    errorMsg: String?,
    lastSyncTime: String,
    onDisconnect: () -> Unit
) {
    ScreenList {
        item {
            AccountHeader(
                label = syncData?.label ?: prefManager.getDeviceLabel() ?: "جهاز مستمع المدفوعات",
                phone = syncData?.phoneNumber ?: prefManager.getDevicePhone() ?: "بدون رقم",
                connected = errorMsg == null && syncData != null
            )
        }
        item {
            SettingsCard(
                rows = listOf(
                    "السيرفر" to (prefManager.getServerUrl() ?: "غير محدد"),
                    "آخر مزامنة" to lastSyncTime,
                    "خدمة الخلفية" to "مفعلة تلقائياً",
                    "التقاط SMS" to "VF-Cash والمرسلين المحددين من الإدارة"
                )
            )
        }
        item {
            Button(
                onClick = {
                    prefManager.clearConfiguration()
                    onDisconnect()
                },
                colors = ButtonDefaults.buttonColors(containerColor = Danger),
                modifier = Modifier
                    .fillMaxWidth()
                    .height(52.dp)
            ) {
                Text("قطع اتصال المحفظة وإعادة الضبط", fontWeight = FontWeight.Bold)
            }
        }
    }
}

@Composable
private fun ScreenList(content: androidx.compose.foundation.lazy.LazyListScope.() -> Unit) {
    LazyColumn(
        modifier = Modifier
            .fillMaxSize()
            .background(OffWhite),
        contentPadding = PaddingValues(start = 16.dp, end = 16.dp, top = 12.dp, bottom = 22.dp),
        verticalArrangement = Arrangement.spacedBy(14.dp),
        content = content
    )
}

@Composable
private fun SyncPill(isSyncing: Boolean, isConnected: Boolean) {
    AssistChip(
        onClick = {},
        label = {
            Text(
                text = when {
                    isSyncing -> "تحديث"
                    isConnected -> "متصل"
                    else -> "غير متصل"
                },
                fontSize = 11.sp,
                fontWeight = FontWeight.Bold
            )
        },
        leadingIcon = {
            Icon(
                imageVector = if (isSyncing) Icons.Filled.Refresh else Icons.Filled.SignalCellularAlt,
                contentDescription = null,
                modifier = Modifier.size(16.dp)
            )
        },
        colors = AssistChipDefaults.assistChipColors(
            containerColor = if (isConnected) Color(0xFFE5F6F4) else Color(0xFFFFECEC),
            labelColor = if (isConnected) Teal else Danger,
            leadingIconContentColor = if (isConnected) Teal else Danger
        ),
        border = null
    )
}

@Composable
private fun AuthorityCard(title: String, subtitle: String, amount: Double, isConnected: Boolean) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(20.dp),
        colors = CardDefaults.cardColors(containerColor = Navy)
    ) {
        Column(modifier = Modifier.padding(18.dp), horizontalAlignment = Alignment.End) {
            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.SpaceBetween, modifier = Modifier.fillMaxWidth()) {
                StatusBadge(connected = isConnected)
                Icon(Icons.Filled.CreditCard, contentDescription = null, tint = Color.White, modifier = Modifier.size(32.dp))
            }
            Spacer(Modifier.height(18.dp))
            Text(title, color = Color.White, fontSize = 20.sp, fontWeight = FontWeight.Black, textAlign = TextAlign.End)
            Text(subtitle, color = Color.White.copy(alpha = 0.75f), fontSize = 13.sp, fontFamily = FontFamily.Monospace)
            Spacer(Modifier.height(14.dp))
            Text("الرصيد الحالي", color = Color.White.copy(alpha = 0.72f), fontSize = 12.sp)
            Text(money(amount), color = Color.White, fontSize = 32.sp, fontWeight = FontWeight.Black)
        }
    }
}

@Composable
private fun NoticeCard(title: String, body: String, color: Color) {
    Card(
        shape = RoundedCornerShape(18.dp),
        colors = CardDefaults.cardColors(containerColor = Color.White),
        modifier = Modifier.fillMaxWidth()
    ) {
        Row(modifier = Modifier.padding(16.dp), verticalAlignment = Alignment.CenterVertically) {
            Box(modifier = Modifier.size(42.dp).clip(CircleShape).background(color.copy(alpha = 0.12f)), contentAlignment = Alignment.Center) {
                Icon(Icons.Filled.Sms, contentDescription = null, tint = color)
            }
            Spacer(Modifier.width(12.dp))
            Column(horizontalAlignment = Alignment.End, modifier = Modifier.weight(1f)) {
                Text(title, color = Navy, fontWeight = FontWeight.Black, fontSize = 15.sp)
                Text(body, color = DarkGray, fontSize = 12.sp, lineHeight = 18.sp, textAlign = TextAlign.End)
            }
        }
    }
}

@Composable
private fun MetricTile(label: String, value: String, icon: ImageVector, modifier: Modifier = Modifier) {
    Card(
        modifier = modifier,
        shape = RoundedCornerShape(16.dp),
        colors = CardDefaults.cardColors(containerColor = Color.White)
    ) {
        Column(modifier = Modifier.padding(14.dp), horizontalAlignment = Alignment.End) {
            Icon(icon, contentDescription = null, tint = Teal, modifier = Modifier.size(22.dp))
            Spacer(Modifier.height(14.dp))
            Text(label, color = DarkGray, fontSize = 12.sp)
            Text(value, color = Navy, fontWeight = FontWeight.Black, fontSize = 17.sp)
        }
    }
}

@Composable
private fun FiltersCard(filters: List<String>) {
    Card(shape = RoundedCornerShape(18.dp), colors = CardDefaults.cardColors(containerColor = Color.White)) {
        Column(modifier = Modifier.padding(16.dp), horizontalAlignment = Alignment.End) {
            Text("مرسلو SMS المعتمدون", color = Navy, fontWeight = FontWeight.Black)
            Spacer(Modifier.height(8.dp))
            Text("أي رسالة من غير الأسماء دي يتم تجاهلها لحماية المطابقة.", color = DarkGray, fontSize = 12.sp, textAlign = TextAlign.End)
            Spacer(Modifier.height(12.dp))
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth()) {
                filters.take(3).forEach { filter ->
                    Surface(color = SoftGray, shape = RoundedCornerShape(10.dp)) {
                        Text(filter, modifier = Modifier.padding(horizontal = 10.dp, vertical = 8.dp), color = Navy, fontFamily = FontFamily.Monospace, fontSize = 12.sp)
                    }
                }
            }
        }
    }
}

@Composable
private fun WalletRow(wallet: OtherDeviceDto, isPrimary: Boolean) {
    Card(shape = RoundedCornerShape(16.dp), colors = CardDefaults.cardColors(containerColor = Color.White)) {
        Row(modifier = Modifier.fillMaxWidth().padding(14.dp), verticalAlignment = Alignment.CenterVertically) {
            Box(modifier = Modifier.size(46.dp).clip(CircleShape).background(if (isPrimary) Color(0xFFE5F6F4) else SoftGray), contentAlignment = Alignment.Center) {
                Icon(Icons.Filled.PhoneAndroid, contentDescription = null, tint = if (isPrimary) Teal else DarkGray)
            }
            Spacer(Modifier.width(12.dp))
            Column(horizontalAlignment = Alignment.End, modifier = Modifier.weight(1f)) {
                Text(wallet.label, color = Navy, fontWeight = FontWeight.Black, fontSize = 14.sp)
                Text(wallet.phoneNumber, color = DarkGray, fontSize = 12.sp, fontFamily = FontFamily.Monospace)
                Spacer(Modifier.height(4.dp))
                StatusBadge(connected = wallet.deviceStatus.equals("Connected", ignoreCase = true))
            }
            Spacer(Modifier.width(10.dp))
            Text(money(wallet.currentBalance), color = Navy, fontWeight = FontWeight.Black, fontSize = 15.sp)
        }
    }
}

@Composable
private fun LimitCard(title: String, received: Double, limit: Double) {
    val ratio = if (limit > 0) (received / limit).toFloat().coerceIn(0f, 1f) else 0f
    Card(shape = RoundedCornerShape(18.dp), colors = CardDefaults.cardColors(containerColor = Color.White)) {
        Column(modifier = Modifier.padding(16.dp), horizontalAlignment = Alignment.End) {
            Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
                Text("${(ratio * 100).toInt()}%", color = Teal, fontWeight = FontWeight.Black)
                Text(title, color = Navy, fontWeight = FontWeight.Black)
            }
            Spacer(Modifier.height(10.dp))
            LinearProgressIndicator(
                progress = ratio,
                color = Teal,
                trackColor = SoftGray,
                modifier = Modifier.fillMaxWidth().height(9.dp).clip(RoundedCornerShape(6.dp))
            )
            Spacer(Modifier.height(8.dp))
            Text("${money(received)} من ${money(limit)}", color = DarkGray, fontSize = 12.sp)
        }
    }
}

@Composable
private fun TransactionSummaryCard(walletLabel: String, lastSyncTime: String, balance: Double) {
    Card(shape = RoundedCornerShape(18.dp), colors = CardDefaults.cardColors(containerColor = Color.White)) {
        Column(modifier = Modifier.padding(16.dp), horizontalAlignment = Alignment.End) {
            Text("آخر حالة معروفة", color = Navy, fontWeight = FontWeight.Black)
            Spacer(Modifier.height(12.dp))
            SummaryLine("المحفظة", walletLabel)
            SummaryLine("آخر مزامنة", lastSyncTime)
            SummaryLine("رصيد المحفظة", money(balance))
            Divider(color = LineGray, modifier = Modifier.padding(vertical = 12.dp))
            Text("سجل العمليات التفصيلي يظهر في لوحة الأدمن بعد مطابقة رسائل SMS وطلبات الشحن.", color = DarkGray, fontSize = 12.sp, textAlign = TextAlign.End)
        }
    }
}

@Composable
private fun AccountHeader(label: String, phone: String, connected: Boolean) {
    Card(shape = RoundedCornerShape(20.dp), colors = CardDefaults.cardColors(containerColor = Color.White)) {
        Column(modifier = Modifier.fillMaxWidth().padding(18.dp), horizontalAlignment = Alignment.CenterHorizontally) {
            Box(modifier = Modifier.size(72.dp).clip(CircleShape).background(Color(0xFFE5F6F4)), contentAlignment = Alignment.Center) {
                Icon(Icons.Filled.AccountCircle, contentDescription = null, tint = Teal, modifier = Modifier.size(44.dp))
            }
            Spacer(Modifier.height(12.dp))
            Text(label, color = Navy, fontWeight = FontWeight.Black, fontSize = 18.sp)
            Text(phone, color = DarkGray, fontFamily = FontFamily.Monospace, fontSize = 13.sp)
            Spacer(Modifier.height(10.dp))
            StatusBadge(connected = connected)
        }
    }
}

@Composable
private fun SettingsCard(rows: List<Pair<String, String>>) {
    Card(shape = RoundedCornerShape(18.dp), colors = CardDefaults.cardColors(containerColor = Color.White)) {
        Column(modifier = Modifier.padding(16.dp)) {
            rows.forEachIndexed { index, row ->
                SummaryLine(row.first, row.second)
                if (index != rows.lastIndex) Divider(color = LineGray, modifier = Modifier.padding(vertical = 10.dp))
            }
        }
    }
}

@Composable
private fun SectionTitle(title: String, subtitle: String) {
    Column(horizontalAlignment = Alignment.End, modifier = Modifier.fillMaxWidth()) {
        Text(title, color = Navy, fontWeight = FontWeight.Black, fontSize = 20.sp)
        Text(subtitle, color = DarkGray, fontSize = 12.sp, textAlign = TextAlign.End)
    }
}

@Composable
private fun SummaryLine(label: String, value: String) {
    Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
        Text(value, color = Navy, fontWeight = FontWeight.Bold, fontSize = 13.sp, textAlign = TextAlign.Start, modifier = Modifier.weight(1f))
        Spacer(Modifier.width(10.dp))
        Text(label, color = DarkGray, fontSize = 12.sp)
    }
}

@Composable
private fun StatusBadge(connected: Boolean) {
    Row(verticalAlignment = Alignment.CenterVertically) {
        Text(
            text = if (connected) "متصل" else "غير متصل",
            color = if (connected) Success else Danger,
            fontSize = 11.sp,
            fontWeight = FontWeight.Bold
        )
        Spacer(Modifier.width(6.dp))
        Box(modifier = Modifier.size(8.dp).clip(CircleShape).background(if (connected) Success else Danger))
    }
}

private fun money(value: Double): String = "${String.format(Locale.US, "%,.2f", value)} ج.م"
