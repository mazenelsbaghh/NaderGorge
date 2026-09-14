package com.nadergorge.paymentlistener.ui.screens

import android.content.Context
import android.widget.Toast
import androidx.compose.foundation.BorderStroke
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
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.selection.selectable
import androidx.compose.foundation.selection.selectableGroup
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.CreditCard
import androidx.compose.material.icons.filled.DeleteOutline
import androidx.compose.material.icons.filled.Payments
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material.icons.filled.SimCard
import androidx.compose.material.icons.filled.SpaceDashboard
import androidx.compose.material.icons.filled.WarningAmber
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.AssistChip
import androidx.compose.material3.AssistChipDefaults
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Divider
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.RadioButton
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.google.gson.JsonParseException
import com.nadergorge.paymentlistener.data.api.ApiClient
import com.nadergorge.paymentlistener.data.api.ApiService
import com.nadergorge.paymentlistener.data.api.SyncStatusRequest
import com.nadergorge.paymentlistener.data.preference.PreferenceManager
import com.nadergorge.paymentlistener.data.preference.WalletPairing
import com.nadergorge.paymentlistener.data.preference.WalletPairingWriteResult
import com.nadergorge.paymentlistener.data.preference.WalletSimBinding
import com.nadergorge.paymentlistener.data.preference.WalletSyncSnapshot
import com.nadergorge.paymentlistener.data.sim.SimSubscription
import com.nadergorge.paymentlistener.data.sim.SimSubscriptionMapping
import com.nadergorge.paymentlistener.data.sim.SimSubscriptionReader
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.async
import kotlinx.coroutines.awaitAll
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.delay
import java.io.IOException
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

private val Navy = Color(0xFF0A1D3D)
private val Teal = Color(0xFF0E8F8F)
private val OffWhite = Color(0xFFF6F7F8)
private val CardWhite = Color(0xFFFCFDFE)
private val SoftGray = Color(0xFFEEF1F4)
private val LineGray = Color(0xFFDCE1E6)
private val DarkGray = Color(0xFF435160)
private val Success = Color(0xFF147A55)
private val Danger = Color(0xFFC83D4D)
private val Warning = Color(0xFF9A6810)
private val SoftTeal = Color(0xFFE5F6F4)
private val SoftWarning = Color(0xFFFFF4D8)

private const val SYNC_INTERVAL_MILLIS = 30_000L

private data class DashboardTab(
    val title: String,
    val icon: ImageVector
)

private data class WalletDisplayState(
    val pairing: WalletPairing,
    val isSyncing: Boolean = false,
    val errorMessage: String? = null,
    val lastSyncedAtMillis: Long? = null
) {
    val isConnected: Boolean
        get() = errorMessage == null && lastSyncedAtMillis != null
}

private data class WalletsTabState(
    val walletStates: List<WalletDisplayState>,
    val canAddWallet: Boolean,
    val walletTokensNeedingAssignment: Set<String>
)

private data class SettingsTabState(
    val walletStates: List<WalletDisplayState>,
    val serverUrl: String?,
    val canAddWallet: Boolean,
    val hasWalletsNeedingAssignment: Boolean
)

private data class ConfirmationCopy(
    val title: String,
    val body: String,
    val confirmLabel: String
)

private data class SimAssignmentCandidate(
    val subscription: SimSubscription,
    val staleWalletNames: List<String>
)

private sealed interface SimAssignmentOutcome {
    data class Saved(val releasedWalletCount: Int) : SimAssignmentOutcome
    data object ActiveWalletConflict : SimAssignmentOutcome
    data object SubscriptionUnavailable : SimAssignmentOutcome
    data object WalletNotFound : SimAssignmentOutcome
    data object SaveFailed : SimAssignmentOutcome
}

private val dashboardTabs = listOf(
    DashboardTab("نظرة عامة", Icons.Filled.SpaceDashboard),
    DashboardTab("المحافظ", Icons.Filled.CreditCard),
    DashboardTab("التحصيلات", Icons.Filled.Payments),
    DashboardTab("الإعدادات", Icons.Filled.Settings)
)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DashboardScreen(
    prefManager: PreferenceManager,
    onAddWallet: () -> Unit,
    onAllWalletsRemoved: () -> Unit
) {
    val context = LocalContext.current
    val subscriptionReader = remember(context) { SimSubscriptionReader(context) }
    var walletStates by remember {
        mutableStateOf(prefManager.getWalletPairings().map(::WalletDisplayState))
    }
    var activeSubscriptions by remember { mutableStateOf(subscriptionReader.activeSubscriptions()) }
    var selectedTab by rememberSaveable { mutableIntStateOf(0) }
    var refreshGeneration by remember { mutableIntStateOf(0) }
    var walletPendingRemoval by remember { mutableStateOf<WalletPairing?>(null) }
    var walletPendingSimAssignment by remember { mutableStateOf<WalletPairing?>(null) }
    var showClearAllConfirmation by remember { mutableStateOf(false) }

    LaunchedEffect(refreshGeneration) {
        while (true) {
            activeSubscriptions = subscriptionReader.activeSubscriptions()
            val storedPairings = prefManager.getWalletPairings()
            val previousStates = walletStates.associateBy { state -> state.pairing.token }
            walletStates = storedPairings.map { pairing ->
                previousStates[pairing.token]
                    ?.copy(pairing = pairing, isSyncing = true)
                    ?: WalletDisplayState(pairing = pairing, isSyncing = true)
            }
            val synchronizedStates = synchronizeWallets(context, prefManager, walletStates)
            val storedTokens = prefManager.getWalletPairings().map(WalletPairing::token).toSet()
            walletStates = synchronizedStates.filter { state -> state.pairing.token in storedTokens }
            delay(SYNC_INTERVAL_MILLIS)
        }
    }

    val pairings = walletStates.map(WalletDisplayState::pairing)
    val availableSubscriptions = activeSubscriptions.filter { subscription ->
        !SimSubscriptionMapping.isAssigned(
            subscription = subscription,
            pairings = pairings
        )
    }
    val walletTokensNeedingAssignment = walletStates
        .filter { state ->
            SimSubscriptionMapping.needsExplicitAssignment(state.pairing, activeSubscriptions)
        }
        .map { state -> state.pairing.token }
        .toSet()
    val hasWalletsNeedingAssignment = walletTokensNeedingAssignment.isNotEmpty()
    val canAddWallet = availableSubscriptions.isNotEmpty() && !hasWalletsNeedingAssignment

    Scaffold(
        containerColor = OffWhite,
        topBar = {
            TopAppBar(
                title = {
                    Column(horizontalAlignment = Alignment.Start) {
                        Text("Massar PAY", color = Navy, fontWeight = FontWeight.Black, fontSize = 20.sp)
                        Text("محافظ هذا الهاتف ورسائل SMS", color = DarkGray, fontSize = 12.sp)
                    }
                },
                actions = {
                    SyncControl(
                        isSyncing = walletStates.any(WalletDisplayState::isSyncing),
                        connectedCount = walletStates.count(WalletDisplayState::isConnected),
                        walletCount = walletStates.size,
                        onRefresh = { refreshGeneration++ }
                    )
                },
                colors = TopAppBarDefaults.topAppBarColors(containerColor = OffWhite)
            )
        },
        bottomBar = {
            NavigationBar(containerColor = CardWhite, tonalElevation = 0.dp) {
                dashboardTabs.forEachIndexed { index, tab ->
                    NavigationBarItem(
                        selected = selectedTab == index,
                        onClick = { selectedTab = index },
                        icon = { Icon(tab.icon, contentDescription = tab.title) },
                        label = { Text(tab.title, fontSize = 10.sp, fontWeight = FontWeight.Bold) },
                        alwaysShowLabel = true
                    )
                }
            }
        }
    ) { innerPadding ->
        Box(modifier = Modifier.padding(innerPadding)) {
            when (selectedTab) {
                0 -> OverviewTab(walletStates)
                1 -> WalletsTab(
                    state = WalletsTabState(
                        walletStates = walletStates,
                        canAddWallet = canAddWallet,
                        walletTokensNeedingAssignment = walletTokensNeedingAssignment
                    ),
                    onAddWallet = onAddWallet,
                    onAssignSim = { pairing -> walletPendingSimAssignment = pairing },
                    onRemoveWallet = { pairing -> walletPendingRemoval = pairing }
                )
                2 -> CollectionsTab(walletStates)
                else -> SettingsTab(
                    state = SettingsTabState(
                        walletStates = walletStates,
                        serverUrl = prefManager.getServerUrl(),
                        canAddWallet = canAddWallet,
                        hasWalletsNeedingAssignment = hasWalletsNeedingAssignment
                    ),
                    onAddWallet = onAddWallet,
                    onClearAll = { showClearAllConfirmation = true }
                )
            }
        }
    }

    walletPendingRemoval?.let { pairing ->
        ConfirmationDialog(
            copy = ConfirmationCopy(
                title = "إزالة ${walletName(pairing)}؟",
                body = "سيتوقف هذا الخط عن إرسال رسائل المحفظة إلى المنصة. لن تتأثر المحافظ الأخرى على الهاتف.",
                confirmLabel = "إزالة المحفظة"
            ),
            onDismiss = { walletPendingRemoval = null },
            onConfirm = {
                prefManager.removeWalletPairing(pairing.token)
                walletStates = walletStates.filterNot { state -> state.pairing.token == pairing.token }
                walletPendingRemoval = null
                if (!prefManager.hasWalletPairings()) onAllWalletsRemoved()
            }
        )
    }

    walletPendingSimAssignment?.let { pairing ->
        val assignmentCandidates = activeSubscriptions.mapNotNull { subscription ->
            val claims = SimSubscriptionMapping.assignmentClaims(
                subscription = subscription,
                pairings = pairings,
                currentWalletToken = pairing.token,
                activeSubscriptions = activeSubscriptions
            )
            if (claims.isBlocked) return@mapNotNull null
            SimAssignmentCandidate(
                subscription = subscription,
                staleWalletNames = pairings
                    .filter { storedPairing -> storedPairing.token in claims.staleWalletTokens }
                    .map(::walletName)
            )
        }
        SimAssignmentDialog(
            wallet = pairing,
            candidates = assignmentCandidates,
            onDismiss = { walletPendingSimAssignment = null },
            onAssign = { candidate ->
                val currentActiveSubscriptions = subscriptionReader.activeSubscriptions()
                when (
                    val outcome = assignWalletToSubscription(
                        preferences = prefManager,
                        walletToken = pairing.token,
                        subscription = candidate.subscription,
                        activeSubscriptions = currentActiveSubscriptions
                    )
                ) {
                    is SimAssignmentOutcome.Saved -> {
                        walletPendingSimAssignment = null
                        refreshGeneration++
                        if (outcome.releasedWalletCount > 0) {
                            Toast.makeText(
                                context,
                                "تم الربط. اختر خطًا من جديد للمحافظ التي تغيّرت شرائحها.",
                                Toast.LENGTH_LONG
                            ).show()
                        }
                    }
                    SimAssignmentOutcome.ActiveWalletConflict -> Toast.makeText(
                        context,
                        "هذا الخط مربوط بمحفظة أخرى بالفعل.",
                        Toast.LENGTH_LONG
                    ).show()
                    SimAssignmentOutcome.SubscriptionUnavailable -> {
                        walletPendingSimAssignment = null
                        refreshGeneration++
                        Toast.makeText(context, "هذا الخط لم يعد متاحًا. أعد الاختيار.", Toast.LENGTH_LONG).show()
                    }
                    SimAssignmentOutcome.WalletNotFound -> {
                        walletPendingSimAssignment = null
                        Toast.makeText(context, "هذه المحفظة لم تعد موجودة على الهاتف.", Toast.LENGTH_LONG).show()
                    }
                    SimAssignmentOutcome.SaveFailed -> Toast.makeText(
                        context,
                        "تعذر حفظ ربط الخط. حاول مرة أخرى.",
                        Toast.LENGTH_LONG
                    ).show()
                }
            }
        )
    }

    if (showClearAllConfirmation) {
        ConfirmationDialog(
            copy = ConfirmationCopy(
                title = "فصل كل محافظ الهاتف؟",
                body = "ستتوقف كل الخطوط عن إرسال رسائل المحافظ حتى تعيد ربطها بكود جديد.",
                confirmLabel = "فصل كل المحافظ"
            ),
            onDismiss = { showClearAllConfirmation = false },
            onConfirm = {
                prefManager.clearWalletPairings()
                walletStates = emptyList()
                showClearAllConfirmation = false
                onAllWalletsRemoved()
            }
        )
    }
}

@Composable
private fun OverviewTab(walletStates: List<WalletDisplayState>) {
    ScreenList {
        item { DeviceSummary(walletStates) }
        item { SectionTitle("محافظ هذا الهاتف", "كل محفظة مستقلة ومرتبطة بخط محدد.") }
        items(walletStates, key = { state -> state.pairing.token }) { state ->
            WalletSummaryRow(state)
        }
        val failedWallets = walletStates.filter { state -> state.errorMessage != null }
        if (failedWallets.isNotEmpty()) {
            item {
                NoticeCard(
                    title = "توجد محافظ تحتاج متابعة",
                    body = failedWallets.joinToString(" • ") { state -> walletName(state.pairing) }
                )
            }
        }
    }
}

@Composable
private fun WalletsTab(
    state: WalletsTabState,
    onAddWallet: () -> Unit,
    onAssignSim: (WalletPairing) -> Unit,
    onRemoveWallet: (WalletPairing) -> Unit
) {
    ScreenList {
        item {
            SectionTitle(
                "محافظ هذا الهاتف",
                "اربط كل محفظة بخطها، وتابع حالتها وحدودها من مكان واحد."
            )
        }
        if (state.walletTokensNeedingAssignment.isNotEmpty()) {
            item {
                NoticeCard(
                    title = "أكمل تعيين الخط أولًا",
                    body = "توجد محفظة بلا خط صالح أو تغيّرت شريحتها. اختر خطها صراحة قبل إضافة محفظة أخرى."
                )
            }
        }
        items(state.walletStates, key = { walletState -> walletState.pairing.token }) { walletState ->
            WalletCard(
                walletState = walletState,
                needsSimAssignment = walletState.pairing.token in state.walletTokensNeedingAssignment,
                onAssignSim = { onAssignSim(walletState.pairing) },
                onRemove = { onRemoveWallet(walletState.pairing) }
            )
        }
        if (state.canAddWallet) {
            item {
                OutlinedButton(
                    onClick = onAddWallet,
                    border = BorderStroke(1.dp, Teal),
                    modifier = Modifier
                        .fillMaxWidth()
                        .heightIn(min = 52.dp)
                ) {
                    Icon(Icons.Filled.Add, contentDescription = null, tint = Teal)
                    Spacer(Modifier.width(8.dp))
                    Text("إضافة محفظة أخرى", color = Teal, fontWeight = FontWeight.Black)
                }
            }
        }
    }
}

@Composable
private fun CollectionsTab(walletStates: List<WalletDisplayState>) {
    ScreenList {
        item { CollectionsSummary(walletStates) }
        item { SectionTitle("تفاصيل التحصيل", "الحدود والأرصدة معروضة لكل محفظة بشكل مستقل.") }
        items(walletStates, key = { state -> state.pairing.token }) { state ->
            WalletCollectionCard(state.pairing)
        }
    }
}

@Composable
private fun SettingsTab(
    state: SettingsTabState,
    onAddWallet: () -> Unit,
    onClearAll: () -> Unit
) {
    val latestSync = state.walletStates.mapNotNull(WalletDisplayState::lastSyncedAtMillis).maxOrNull()
    ScreenList {
        item { SectionTitle("إعدادات الجهاز", "إدارة الربط وخدمات المزامنة لهذا الهاتف.") }
        item {
            SettingsCard(
                rows = listOf(
                    "المحافظ المرتبطة" to state.walletStates.size.toString(),
                    "المتصلة الآن" to state.walletStates.count(WalletDisplayState::isConnected).toString(),
                    "آخر مزامنة" to latestSync?.let(::formatSyncTime).orEmpty().ifBlank { "لم تتم بعد" },
                    "السيرفر" to (state.serverUrl ?: "غير محدد"),
                    "خدمة الخلفية" to "مفعلة تلقائيًا"
                )
            )
        }
        if (state.hasWalletsNeedingAssignment) {
            item {
                NoticeCard(
                    title = "ربط خط مطلوب",
                    body = "افتح تبويب المحافظ واختر خطًا لكل محفظة بلا خط صالح أو تغيّرت شريحتها."
                )
            }
        }
        if (state.canAddWallet) {
            item {
                OutlinedButton(
                    onClick = onAddWallet,
                    modifier = Modifier
                        .fillMaxWidth()
                        .heightIn(min = 52.dp)
                ) {
                    Icon(Icons.Filled.Add, contentDescription = null)
                    Spacer(Modifier.width(8.dp))
                    Text("إضافة محفظة أخرى", fontWeight = FontWeight.Black)
                }
            }
        }
        item {
            Button(
                onClick = onClearAll,
                colors = ButtonDefaults.buttonColors(containerColor = Danger),
                modifier = Modifier
                    .fillMaxWidth()
                    .heightIn(min = 52.dp)
            ) {
                Text("فصل كل محافظ الهاتف", fontWeight = FontWeight.Black)
            }
        }
    }
}

@Composable
private fun DeviceSummary(walletStates: List<WalletDisplayState>) {
    val totalBalance = walletStates.sumOf { state -> state.pairing.currentBalance }
    val connectedCount = walletStates.count(WalletDisplayState::isConnected)
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(22.dp),
        colors = CardDefaults.cardColors(containerColor = Navy)
    ) {
        Column(modifier = Modifier.padding(20.dp)) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text("حالة هذا الهاتف", color = CardWhite.copy(alpha = 0.76f), fontSize = 13.sp)
                Text(
                    "$connectedCount من ${walletStates.size} متصلة",
                    color = CardWhite,
                    fontWeight = FontWeight.Black,
                    fontSize = 13.sp
                )
            }
            Spacer(Modifier.height(22.dp))
            Text("إجمالي أرصدة المحافظ", color = CardWhite.copy(alpha = 0.72f), fontSize = 12.sp)
            Text(money(totalBalance), color = CardWhite, fontSize = 31.sp, fontWeight = FontWeight.Black)
            Spacer(Modifier.height(16.dp))
            Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                SummaryAmount(
                    label = "تحصيل اليوم",
                    amount = walletStates.sumOf { state -> state.pairing.dailyReceived },
                    modifier = Modifier.weight(1f)
                )
                SummaryAmount(
                    label = "تحصيل الشهر",
                    amount = walletStates.sumOf { state -> state.pairing.monthlyReceived },
                    modifier = Modifier.weight(1f)
                )
            }
        }
    }
}

@Composable
private fun SummaryAmount(label: String, amount: Double, modifier: Modifier = Modifier) {
    Column(modifier = modifier) {
        Text(label, color = CardWhite.copy(alpha = 0.68f), fontSize = 11.sp)
        Text(money(amount), color = CardWhite, fontSize = 14.sp, fontWeight = FontWeight.Black)
    }
}

@Composable
private fun WalletSummaryRow(walletState: WalletDisplayState) {
    val pairing = walletState.pairing
    Surface(
        color = CardWhite,
        shape = RoundedCornerShape(18.dp),
        border = BorderStroke(1.dp, LineGray),
        modifier = Modifier.fillMaxWidth()
    ) {
        Row(
            modifier = Modifier.padding(15.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            SimIcon()
            Column(
                modifier = Modifier
                    .weight(1f)
                    .padding(horizontal = 12.dp)
            ) {
                Text(walletName(pairing), color = Navy, fontWeight = FontWeight.Black, fontSize = 14.sp)
                Text(pairing.phoneNumber.ifBlank { "لم يصل رقم المحفظة" }, color = DarkGray, fontSize = 12.sp, fontFamily = FontFamily.Monospace)
                Text(simDescription(pairing), color = DarkGray, fontSize = 12.sp)
            }
            Column(horizontalAlignment = Alignment.End) {
                Text(money(pairing.currentBalance), color = Navy, fontWeight = FontWeight.Black, fontSize = 14.sp)
                ConnectionLabel(walletState)
            }
        }
    }
}

@Composable
private fun WalletCard(
    walletState: WalletDisplayState,
    needsSimAssignment: Boolean,
    onAssignSim: () -> Unit,
    onRemove: () -> Unit
) {
    val pairing = walletState.pairing
    Card(
        shape = RoundedCornerShape(20.dp),
        colors = CardDefaults.cardColors(containerColor = CardWhite),
        border = BorderStroke(1.dp, if (needsSimAssignment) Warning.copy(alpha = 0.5f) else LineGray),
        modifier = Modifier.fillMaxWidth()
    ) {
        Column(modifier = Modifier.padding(17.dp)) {
            Row(modifier = Modifier.fillMaxWidth(), verticalAlignment = Alignment.Top) {
                SimIcon()
                Column(
                    modifier = Modifier
                        .weight(1f)
                        .padding(horizontal = 12.dp)
                ) {
                    Text(walletName(pairing), color = Navy, fontWeight = FontWeight.Black, fontSize = 16.sp)
                    Text(pairing.phoneNumber.ifBlank { "لم يصل رقم المحفظة" }, color = DarkGray, fontSize = 13.sp, fontFamily = FontFamily.Monospace)
                    Text(
                        if (needsSimAssignment) "الخط المحفوظ غير موجود أو تغيّرت الشريحة" else simDescription(pairing),
                        color = if (needsSimAssignment) Warning else DarkGray,
                        fontSize = 12.sp,
                        fontWeight = FontWeight.Bold
                    )
                }
                Column(horizontalAlignment = Alignment.End) {
                    ConnectionLabel(walletState)
                    Text(
                        if (pairing.isActive) "مفعلة" else "موقوفة من الإدارة",
                        color = if (pairing.isActive) Success else Warning,
                        fontSize = 11.sp,
                        fontWeight = FontWeight.Bold
                    )
                }
            }

            Divider(color = LineGray, modifier = Modifier.padding(vertical = 15.dp))
            Text("الرصيد المسجل", color = DarkGray, fontSize = 12.sp)
            Text(money(pairing.currentBalance), color = Navy, fontWeight = FontWeight.Black, fontSize = 25.sp)
            Spacer(Modifier.height(16.dp))
            WalletLimit("الحد اليومي", pairing.dailyReceived, pairing.dailyLimit)
            Spacer(Modifier.height(12.dp))
            WalletLimit("الحد الشهري", pairing.monthlyReceived, pairing.monthlyLimit)

            walletState.errorMessage?.let { message ->
                Text(
                    message,
                    color = Danger,
                    fontSize = 12.sp,
                    fontWeight = FontWeight.Bold,
                    modifier = Modifier.padding(top = 14.dp)
                )
            }

            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(top = 16.dp),
                horizontalArrangement = Arrangement.spacedBy(10.dp)
            ) {
                if (needsSimAssignment) {
                    Button(
                        onClick = onAssignSim,
                        modifier = Modifier
                            .weight(1f)
                            .heightIn(min = 48.dp)
                    ) {
                        Icon(Icons.Filled.SimCard, contentDescription = null)
                        Spacer(Modifier.width(7.dp))
                        Text("ربط هذه المحفظة بخط", fontWeight = FontWeight.Black, fontSize = 12.sp)
                    }
                }
                OutlinedButton(
                    onClick = onRemove,
                    border = BorderStroke(1.dp, Danger.copy(alpha = 0.55f)),
                    modifier = Modifier
                        .weight(if (needsSimAssignment) 0.7f else 1f)
                        .heightIn(min = 48.dp)
                ) {
                    Icon(Icons.Filled.DeleteOutline, contentDescription = null, tint = Danger)
                    Spacer(Modifier.width(6.dp))
                    Text("إزالة", color = Danger, fontWeight = FontWeight.Bold)
                }
            }
        }
    }
}

@Composable
private fun CollectionsSummary(walletStates: List<WalletDisplayState>) {
    Surface(
        color = SoftTeal,
        shape = RoundedCornerShape(20.dp),
        modifier = Modifier.fillMaxWidth()
    ) {
        Column(modifier = Modifier.padding(18.dp)) {
            Text("إجمالي تحصيل محافظ الهاتف", color = Navy, fontWeight = FontWeight.Black, fontSize = 16.sp)
            Spacer(Modifier.height(14.dp))
            Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                CollectionMetric("اليوم", walletStates.sumOf { it.pairing.dailyReceived }, Modifier.weight(1f))
                CollectionMetric("هذا الشهر", walletStates.sumOf { it.pairing.monthlyReceived }, Modifier.weight(1f))
            }
        }
    }
}

@Composable
private fun CollectionMetric(label: String, amount: Double, modifier: Modifier) {
    Column(modifier = modifier) {
        Text(label, color = DarkGray, fontSize = 12.sp)
        Text(money(amount), color = Navy, fontWeight = FontWeight.Black, fontSize = 18.sp)
    }
}

@Composable
private fun WalletCollectionCard(pairing: WalletPairing) {
    Surface(
        color = CardWhite,
        shape = RoundedCornerShape(18.dp),
        border = BorderStroke(1.dp, LineGray),
        modifier = Modifier.fillMaxWidth()
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            Text(walletName(pairing), color = Navy, fontWeight = FontWeight.Black, fontSize = 15.sp)
            Text(simDescription(pairing), color = DarkGray, fontSize = 12.sp)
            Spacer(Modifier.height(16.dp))
            WalletLimit("الحد اليومي", pairing.dailyReceived, pairing.dailyLimit)
            Spacer(Modifier.height(14.dp))
            WalletLimit("الحد الشهري", pairing.monthlyReceived, pairing.monthlyLimit)
        }
    }
}

@Composable
private fun WalletLimit(label: String, received: Double, limit: Double) {
    val ratio = if (limit > 0) (received / limit).toFloat().coerceIn(0f, 1f) else 0f
    Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
        Text(label, color = DarkGray, fontSize = 12.sp, fontWeight = FontWeight.Bold)
        Text("${money(received)} / ${money(limit)}", color = Navy, fontSize = 11.sp, fontWeight = FontWeight.Bold)
    }
    Spacer(Modifier.height(7.dp))
    LinearProgressIndicator(
        progress = ratio,
        color = Teal,
        trackColor = SoftGray,
        modifier = Modifier
            .fillMaxWidth()
            .height(8.dp)
            .background(SoftGray, RoundedCornerShape(5.dp))
    )
}

@Composable
private fun ConnectionLabel(walletState: WalletDisplayState) {
    val label: String
    val color: Color
    when {
        walletState.isSyncing -> {
            label = "تحديث"
            color = DarkGray
        }
        walletState.isConnected -> {
            label = "متصلة"
            color = Success
        }
        else -> {
            label = "غير متصلة"
            color = Danger
        }
    }
    Row(verticalAlignment = Alignment.CenterVertically) {
        Box(modifier = Modifier.size(7.dp).background(color, CircleShape))
        Spacer(Modifier.width(5.dp))
        Text(label, color = color, fontSize = 11.sp, fontWeight = FontWeight.Bold)
    }
}

@Composable
private fun SimIcon() {
    Surface(color = SoftTeal, shape = CircleShape, modifier = Modifier.size(44.dp)) {
        Icon(
            Icons.Filled.SimCard,
            contentDescription = null,
            tint = Teal,
            modifier = Modifier.padding(10.dp)
        )
    }
}

@Composable
private fun NoticeCard(title: String, body: String) {
    Surface(
        color = SoftWarning,
        shape = RoundedCornerShape(18.dp),
        modifier = Modifier.fillMaxWidth()
    ) {
        Row(modifier = Modifier.padding(16.dp), verticalAlignment = Alignment.CenterVertically) {
            Icon(Icons.Filled.WarningAmber, contentDescription = null, tint = Warning)
            Column(modifier = Modifier.padding(start = 12.dp)) {
                Text(title, color = Navy, fontWeight = FontWeight.Black, fontSize = 14.sp)
                Text(body, color = DarkGray, fontSize = 12.sp, lineHeight = 18.sp)
            }
        }
    }
}

@Composable
private fun SettingsCard(rows: List<Pair<String, String>>) {
    Surface(
        color = CardWhite,
        shape = RoundedCornerShape(18.dp),
        border = BorderStroke(1.dp, LineGray),
        modifier = Modifier.fillMaxWidth()
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            rows.forEachIndexed { index, row ->
                Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                    Text(
                        row.first,
                        color = DarkGray,
                        fontSize = 12.sp,
                        maxLines = 1,
                        modifier = Modifier.weight(0.4f)
                    )
                    Spacer(Modifier.width(10.dp))
                    Text(
                        row.second,
                        color = Navy,
                        fontWeight = FontWeight.Bold,
                        fontSize = 12.sp,
                        textAlign = TextAlign.End,
                        maxLines = 2,
                        overflow = TextOverflow.Ellipsis,
                        modifier = Modifier.weight(0.6f)
                    )
                }
                if (index != rows.lastIndex) Divider(color = LineGray, modifier = Modifier.padding(vertical = 11.dp))
            }
        }
    }
}

@Composable
private fun SectionTitle(title: String, subtitle: String) {
    Column(modifier = Modifier.fillMaxWidth()) {
        Text(title, color = Navy, fontWeight = FontWeight.Black, fontSize = 20.sp)
        Text(subtitle, color = DarkGray, fontSize = 12.sp, lineHeight = 18.sp)
    }
}

@Composable
private fun SyncControl(
    isSyncing: Boolean,
    connectedCount: Int,
    walletCount: Int,
    onRefresh: () -> Unit
) {
    AssistChip(
        onClick = onRefresh,
        label = {
            Text(
                if (isSyncing) "تحديث" else "$connectedCount/$walletCount",
                fontSize = 11.sp,
                fontWeight = FontWeight.Black
            )
        },
        leadingIcon = {
            if (isSyncing) {
                CircularProgressIndicator(strokeWidth = 2.dp, modifier = Modifier.size(15.dp))
            } else {
                Icon(Icons.Filled.Refresh, contentDescription = "تحديث المحافظ", modifier = Modifier.size(16.dp))
            }
        },
        colors = AssistChipDefaults.assistChipColors(
            containerColor = SoftTeal,
            labelColor = Navy,
            leadingIconContentColor = Teal
        ),
        border = null,
        modifier = Modifier.heightIn(min = 44.dp)
    )
}

@Composable
private fun ScreenList(content: androidx.compose.foundation.lazy.LazyListScope.() -> Unit) {
    LazyColumn(
        modifier = Modifier
            .fillMaxSize()
            .background(OffWhite),
        contentPadding = PaddingValues(start = 16.dp, end = 16.dp, top = 12.dp, bottom = 24.dp),
        verticalArrangement = Arrangement.spacedBy(14.dp),
        content = content
    )
}

@Composable
private fun ConfirmationDialog(
    copy: ConfirmationCopy,
    onDismiss: () -> Unit,
    onConfirm: () -> Unit
) {
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(copy.title, color = Navy, fontWeight = FontWeight.Black) },
        text = { Text(copy.body, color = DarkGray, lineHeight = 21.sp) },
        confirmButton = {
            Button(
                onClick = onConfirm,
                colors = ButtonDefaults.buttonColors(containerColor = Danger),
                modifier = Modifier.heightIn(min = 48.dp)
            ) {
                Text(copy.confirmLabel, fontWeight = FontWeight.Black)
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss, modifier = Modifier.heightIn(min = 48.dp)) {
                Text("إلغاء", fontWeight = FontWeight.Bold)
            }
        },
        containerColor = CardWhite
    )
}

@Composable
private fun SimAssignmentDialog(
    wallet: WalletPairing,
    candidates: List<SimAssignmentCandidate>,
    onDismiss: () -> Unit,
    onAssign: (SimAssignmentCandidate) -> Unit
) {
    var selectedSubscriptionId by remember(wallet.token) { mutableStateOf<Int?>(null) }
    val selectedCandidate = candidates.firstOrNull { candidate ->
        candidate.subscription.subscriptionId == selectedSubscriptionId
    }
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("اختر خط ${walletName(wallet)}", color = Navy, fontWeight = FontWeight.Black) },
        text = {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .heightIn(max = 360.dp)
                    .verticalScroll(rememberScrollState())
                    .selectableGroup(),
                verticalArrangement = Arrangement.spacedBy(10.dp)
            ) {
                Text("لن يتم اختيار أي خط تلقائيًا.", color = DarkGray, fontSize = 13.sp)
                candidates.forEach { candidate ->
                    SimAssignmentOption(
                        subscription = candidate.subscription,
                        selected = selectedSubscriptionId == candidate.subscription.subscriptionId,
                        onSelect = { selectedSubscriptionId = candidate.subscription.subscriptionId }
                    )
                }
                selectedCandidate
                    ?.staleWalletNames
                    ?.takeIf(List<String>::isNotEmpty)
                    ?.let { walletNames ->
                        Text(
                            "عند التأكيد سيُلغى تعيين الخط القديم لـ ${walletNames.joinToString("، ")}، وستحتاج اختيار خط لها من جديد.",
                            color = Warning,
                            fontSize = 12.sp,
                            fontWeight = FontWeight.Bold
                        )
                    }
                if (candidates.isEmpty()) {
                    Text("لا يوجد خط غير مربوط متاح حاليًا.", color = Danger, fontWeight = FontWeight.Bold)
                }
            }
        },
        confirmButton = {
            Button(
                onClick = { selectedCandidate?.let(onAssign) },
                enabled = selectedCandidate != null,
                modifier = Modifier.heightIn(min = 48.dp)
            ) {
                Text("ربط الخط المختار", fontWeight = FontWeight.Black)
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss, modifier = Modifier.heightIn(min = 48.dp)) {
                Text("إلغاء", fontWeight = FontWeight.Bold)
            }
        },
        containerColor = CardWhite
    )
}

private fun assignWalletToSubscription(
    preferences: PreferenceManager,
    walletToken: String,
    subscription: SimSubscription,
    activeSubscriptions: List<SimSubscription>
): SimAssignmentOutcome {
    if (activeSubscriptions.none { active ->
            active.subscriptionId == subscription.subscriptionId &&
                active.slotIndex == subscription.slotIndex
        }
    ) {
        return SimAssignmentOutcome.SubscriptionUnavailable
    }
    val pairings = preferences.getWalletPairings()
    val currentPairing = pairings.firstOrNull { pairing -> pairing.token == walletToken }
        ?: return SimAssignmentOutcome.WalletNotFound
    val claims = SimSubscriptionMapping.assignmentClaims(
        subscription,
        pairings,
        walletToken,
        activeSubscriptions
    )
    if (claims.isBlocked) return SimAssignmentOutcome.ActiveWalletConflict
    val stalePairings = pairings.filter { pairing -> pairing.token in claims.staleWalletTokens }
    return persistSimAssignment(preferences, currentPairing.token, subscription, stalePairings)
}

private fun persistSimAssignment(
    preferences: PreferenceManager,
    currentWalletToken: String,
    subscription: SimSubscription,
    stalePairings: List<WalletPairing>
): SimAssignmentOutcome {
    val releasedPairings = releaseStalePairings(preferences, stalePairings)
        ?: return SimAssignmentOutcome.SaveFailed
    return when (
        preferences.updateWalletSimBinding(
            currentWalletToken,
            subscription.toWalletSimBinding()
        )
    ) {
        WalletPairingWriteResult.UPDATED,
        WalletPairingWriteResult.UNCHANGED -> SimAssignmentOutcome.Saved(releasedPairings.size)
        WalletPairingWriteResult.SIM_CONFLICT -> {
            rollbackReleasedPairings(preferences, releasedPairings)
            SimAssignmentOutcome.ActiveWalletConflict
        }
        WalletPairingWriteResult.NOT_FOUND -> {
            rollbackReleasedPairings(preferences, releasedPairings)
            SimAssignmentOutcome.WalletNotFound
        }
        else -> {
            rollbackReleasedPairings(preferences, releasedPairings)
            SimAssignmentOutcome.SaveFailed
        }
    }
}

private fun releaseStalePairings(
    preferences: PreferenceManager,
    stalePairings: List<WalletPairing>
): List<WalletPairing>? {
    val releasedPairings = mutableListOf<WalletPairing>()
    stalePairings.forEach { stalePairing ->
        val currentPairing = preferences.getWalletPairing(stalePairing.token)
        if (currentPairing == null) {
            rollbackReleasedPairings(preferences, releasedPairings)
            return null
        }
        val writeResult = preferences.updateWalletSimBinding(
            currentPairing.token,
            WalletSimBinding.Unassigned
        )
        if (writeResult != WalletPairingWriteResult.UPDATED &&
            writeResult != WalletPairingWriteResult.UNCHANGED
        ) {
            rollbackReleasedPairings(preferences, releasedPairings)
            return null
        }
        releasedPairings += currentPairing
    }
    return releasedPairings
}

private fun rollbackReleasedPairings(
    preferences: PreferenceManager,
    releasedPairings: List<WalletPairing>
) {
    releasedPairings.asReversed().forEach { releasedPairing ->
        preferences.updateWalletSimBinding(releasedPairing.token, releasedPairing.simBinding())
    }
}

private fun SimSubscription.toWalletSimBinding(): WalletSimBinding = WalletSimBinding(
    subscriptionId = subscriptionId,
    simSlotIndex = slotIndex,
    simLabel = displayName.ifBlank { "SIM ${slotIndex + 1}" },
    carrierLabel = carrierName.ifBlank { displayName }
)

@Composable
private fun SimAssignmentOption(
    subscription: SimSubscription,
    selected: Boolean,
    onSelect: () -> Unit
) {
    Surface(
        color = if (selected) SoftTeal else CardWhite,
        shape = RoundedCornerShape(14.dp),
        border = BorderStroke(1.dp, if (selected) Teal else LineGray),
        modifier = Modifier
            .fillMaxWidth()
            .heightIn(min = 64.dp)
            .selectable(
                selected = selected,
                role = Role.RadioButton,
                onClick = onSelect
            )
    ) {
        Row(
            modifier = Modifier.padding(horizontal = 12.dp, vertical = 10.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            RadioButton(selected = selected, onClick = null)
            Column(modifier = Modifier.padding(start = 10.dp)) {
                Text("SIM ${subscription.slotIndex + 1}", color = Navy, fontWeight = FontWeight.Black)
                Text(
                    subscription.carrierName.ifBlank { subscription.displayName.ifBlank { "شبكة غير معروفة" } },
                    color = DarkGray,
                    fontSize = 12.sp
                )
            }
        }
    }
}

private suspend fun synchronizeWallets(
    context: Context,
    preferences: PreferenceManager,
    walletStates: List<WalletDisplayState>
): List<WalletDisplayState> {
    val apiService = ApiClient.getApiService(context)
        ?: return walletStates.map { state ->
            state.copy(isSyncing = false, errorMessage = "إعداد اتصال المنصة غير مكتمل.")
        }

    return coroutineScope {
        walletStates.map { state ->
            async(Dispatchers.IO) { synchronizeWallet(apiService, preferences, state) }
        }.awaitAll()
    }
}

private suspend fun synchronizeWallet(
    apiService: ApiService,
    preferences: PreferenceManager,
    walletState: WalletDisplayState
): WalletDisplayState {
    return try {
        val response = apiService.syncStatus(walletState.pairing.token, SyncStatusRequest(null))
        val walletStatus = response.body()?.data
        if (!response.isSuccessful || response.body()?.success != true || walletStatus == null) {
            walletState.copy(
                isSyncing = false,
                errorMessage = response.body()?.message ?: "تعذر مزامنة هذه المحفظة."
            )
        } else {
            val snapshot = WalletSyncSnapshot(
                phoneNumber = walletStatus.phoneNumber,
                label = walletStatus.label,
                smsSenderFilters = walletStatus.smsSenderFilters,
                currentBalance = walletStatus.currentBalance,
                dailyLimit = walletStatus.dailyLimit,
                monthlyLimit = walletStatus.monthlyLimit,
                dailyReceived = walletStatus.dailyReceived,
                monthlyReceived = walletStatus.monthlyReceived,
                isActive = walletStatus.isActive
            )
            preferences.updateWalletSync(walletState.pairing.token, snapshot)
            walletState.copy(
                pairing = preferences.getWalletPairing(walletState.pairing.token) ?: walletState.pairing,
                isSyncing = false,
                errorMessage = null,
                lastSyncedAtMillis = System.currentTimeMillis()
            )
        }
    } catch (_: IOException) {
        walletState.copy(isSyncing = false, errorMessage = "تعذر الاتصال بالمنصة. تحقق من الإنترنت.")
    } catch (_: JsonParseException) {
        walletState.copy(isSyncing = false, errorMessage = "وصلت استجابة غير صالحة من المنصة.")
    } catch (cancellation: CancellationException) {
        throw cancellation
    }
}

private fun walletName(pairing: WalletPairing): String = pairing.label.ifBlank { "محفظة ${pairing.phoneNumber}" }

private fun simDescription(pairing: WalletPairing): String {
    val slot = pairing.simSlotIndex?.let { slotIndex -> "SIM ${slotIndex + 1}" }
        ?: return "لم يتم تعيين خط"
    val carrier = pairing.carrierLabel.orEmpty().ifBlank { pairing.simLabel.orEmpty() }
    return listOf(slot, carrier).filter(String::isNotBlank).joinToString(" · ")
}

private fun formatSyncTime(timestampMillis: Long): String =
    SimpleDateFormat("hh:mm a", Locale("ar", "EG")).format(Date(timestampMillis))

private fun money(amount: Double): String = "${String.format(Locale.US, "%,.2f", amount)} ج.م"
