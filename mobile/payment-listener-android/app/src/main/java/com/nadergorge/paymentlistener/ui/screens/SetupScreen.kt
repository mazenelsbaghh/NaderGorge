package com.nadergorge.paymentlistener.ui.screens

import android.content.Context
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.selection.selectable
import androidx.compose.foundation.selection.selectableGroup
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.SimCard
import androidx.compose.material3.Button
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.RadioButton
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextDirection
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.google.gson.Gson
import com.google.gson.JsonParseException
import com.nadergorge.paymentlistener.R
import com.nadergorge.paymentlistener.data.api.ApiClient
import com.nadergorge.paymentlistener.data.api.SyncStatusRequest
import com.nadergorge.paymentlistener.data.api.SyncStatusResponse
import com.nadergorge.paymentlistener.data.preference.PreferenceManager
import com.nadergorge.paymentlistener.data.preference.WalletPairing
import com.nadergorge.paymentlistener.data.preference.WalletPairingWriteResult
import com.nadergorge.paymentlistener.data.sim.SimSubscription
import com.nadergorge.paymentlistener.data.sim.SimSubscriptionMapping
import com.nadergorge.paymentlistener.data.sim.SimSubscriptionReader
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.io.IOException
import java.util.Locale

private val SetupNavy = Color(0xFF0A1D3D)
private val SetupTeal = Color(0xFF0E8F8F)
private val SetupCanvas = Color(0xFFF6F7F8)
private val SetupLine = Color(0xFFDCE1E6)
private val SetupMuted = Color(0xFF52606D)
private val SetupSoftTeal = Color(0xFFE5F6F4)

private data class PairingAttempt(
    val serverUrl: String,
    val token: String,
    val subscription: SimSubscription
)

private sealed interface PairingOutcome {
    data object Saved : PairingOutcome
    data class Rejected(val message: String) : PairingOutcome
}

private data class PairingApiError(val message: String?)

@Composable
fun SetupScreen(
    prefManager: PreferenceManager,
    onSetupSuccess: () -> Unit,
    onCancel: (() -> Unit)? = null
) {
    val context = LocalContext.current
    val defaultServerUrl = stringResource(R.string.default_server_url)
    val subscriptionReader = remember(context) { SimSubscriptionReader(context) }
    val gson = remember { Gson() }
    val scope = rememberCoroutineScope()
    var subscriptions by remember { mutableStateOf(subscriptionReader.activeSubscriptions()) }
    var pairingCode by remember { mutableStateOf("") }
    var selectedSubscriptionId by remember { mutableStateOf<Int?>(null) }
    var isPairing by remember { mutableStateOf(false) }
    var errorMessage by remember { mutableStateOf<String?>(null) }
    val storedPairings = prefManager.getWalletPairings()

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(SetupCanvas)
            .verticalScroll(rememberScrollState())
            .navigationBarsPadding()
            .imePadding()
            .padding(horizontal = 20.dp, vertical = 24.dp),
        verticalArrangement = Arrangement.Center
    ) {
        SetupHeader(hasExistingWallets = prefManager.hasWalletPairings(), onCancel = onCancel)
        Spacer(Modifier.height(28.dp))

        Text("1. اختر الخط المرتبط بالمحفظة", color = SetupNavy, fontWeight = FontWeight.Black, fontSize = 17.sp)
        Text(
            "لا يتم اختيار أي خط تلقائيًا. كل خط يمكن ربطه بمحفظة واحدة فقط.",
            color = SetupMuted,
            fontSize = 13.sp,
            modifier = Modifier.padding(top = 5.dp, bottom = 12.dp)
        )

        Column(
            modifier = Modifier
                .fillMaxWidth()
                .selectableGroup(),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            subscriptions.forEach { subscription ->
                val isPaired = SimSubscriptionMapping.isAssigned(subscription, storedPairings)
                SimSubscriptionOption(
                    subscription = subscription,
                    selected = selectedSubscriptionId == subscription.subscriptionId,
                    paired = isPaired,
                    onSelect = {
                        selectedSubscriptionId = subscription.subscriptionId
                        errorMessage = null
                    }
                )
            }
        }

        if (subscriptions.isEmpty()) {
            NoSubscriptionsNotice {
                subscriptions = subscriptionReader.activeSubscriptions()
                selectedSubscriptionId = null
            }
        }

        Spacer(Modifier.height(24.dp))
        Text("2. أدخل كود ربط المحفظة", color = SetupNavy, fontWeight = FontWeight.Black, fontSize = 17.sp)
        Text(
            "ستجد الكود داخل لوحة إدارة محافظ الشحن.",
            color = SetupMuted,
            fontSize = 13.sp,
            modifier = Modifier.padding(top = 5.dp, bottom = 12.dp)
        )
        OutlinedTextField(
            value = pairingCode,
            onValueChange = { enteredCode ->
                pairingCode = normalizePairingCode(enteredCode)
                errorMessage = null
            },
            label = { Text("كود الربط") },
            placeholder = { Text("8 أرقام وحروف") },
            singleLine = true,
            keyboardOptions = KeyboardOptions(
                keyboardType = KeyboardType.Ascii,
                imeAction = ImeAction.Done
            ),
            textStyle = TextStyle(
                fontWeight = FontWeight.Black,
                fontSize = 18.sp,
                textDirection = TextDirection.Ltr
            ),
            modifier = Modifier.fillMaxWidth()
        )

        errorMessage?.let { message ->
            Text(
                text = message,
                color = MaterialTheme.colorScheme.error,
                fontSize = 13.sp,
                fontWeight = FontWeight.Bold,
                textAlign = TextAlign.Start,
                modifier = Modifier.padding(top = 12.dp)
            )
        }

        Spacer(Modifier.height(24.dp))
        Button(
            onClick = {
                val subscription = subscriptions.firstOrNull {
                    it.subscriptionId == selectedSubscriptionId
                }
                if (subscription == null) {
                    errorMessage = "اختر الخط الذي يستقبل رسائل هذه المحفظة."
                    return@Button
                }
                if (pairingCode.isBlank()) {
                    errorMessage = "اكتب كود الربط قبل المتابعة."
                    return@Button
                }

                isPairing = true
                scope.launch {
                    val attempt = PairingAttempt(defaultServerUrl, pairingCode, subscription)
                    when (val outcome = pairWallet(context, prefManager, attempt, gson)) {
                        PairingOutcome.Saved -> onSetupSuccess()
                        is PairingOutcome.Rejected -> errorMessage = outcome.message
                    }
                    isPairing = false
                }
            },
            enabled = !isPairing && selectedSubscriptionId != null && pairingCode.isNotBlank(),
            modifier = Modifier
                .fillMaxWidth()
                .heightIn(min = 54.dp)
        ) {
            if (isPairing) {
                CircularProgressIndicator(
                    color = MaterialTheme.colorScheme.onPrimary,
                    strokeWidth = 2.dp,
                    modifier = Modifier.size(24.dp)
                )
            } else {
                Text("ربط المحفظة بالخط المختار", fontWeight = FontWeight.Black, fontSize = 15.sp)
            }
        }
    }
}

@Composable
private fun SetupHeader(hasExistingWallets: Boolean, onCancel: (() -> Unit)?) {
    Column(modifier = Modifier.fillMaxWidth(), horizontalAlignment = Alignment.Start) {
        if (onCancel != null) {
            TextButton(onClick = onCancel, modifier = Modifier.heightIn(min = 48.dp)) {
                Text("العودة إلى المحافظ", color = SetupTeal, fontWeight = FontWeight.Bold)
            }
        }
        Text(
            text = if (hasExistingWallets) "ربط محفظة أخرى" else "ربط أول محفظة",
            color = SetupNavy,
            fontSize = 28.sp,
            fontWeight = FontWeight.Black
        )
        Text(
            "اربط كل محفظة بالخط الذي يستقبل رسائلها حتى تصل كل عملية إلى حسابها الصحيح.",
            color = SetupMuted,
            fontSize = 14.sp,
            lineHeight = 21.sp,
            modifier = Modifier.padding(top = 7.dp)
        )
    }
}

@Composable
private fun SimSubscriptionOption(
    subscription: SimSubscription,
    selected: Boolean,
    paired: Boolean,
    onSelect: () -> Unit
) {
    val slotLabel = "SIM ${subscription.slotIndex + 1}"
    val carrierLabel = subscription.carrierName.ifBlank {
        subscription.displayName.ifBlank { "شبكة غير معروفة" }
    }
    Surface(
        color = if (selected) SetupSoftTeal else Color.White,
        shape = RoundedCornerShape(18.dp),
        border = BorderStroke(1.dp, if (selected) SetupTeal else SetupLine),
        modifier = Modifier
            .fillMaxWidth()
            .heightIn(min = 76.dp)
            .selectable(
                selected = selected,
                enabled = !paired,
                role = Role.RadioButton,
                onClick = onSelect
            )
    ) {
        Row(
            modifier = Modifier.padding(horizontal = 16.dp, vertical = 14.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Surface(
                color = if (paired) SetupCanvas else SetupSoftTeal,
                shape = CircleShape,
                modifier = Modifier.size(44.dp)
            ) {
                Icon(
                    Icons.Filled.SimCard,
                    contentDescription = null,
                    tint = if (paired) SetupMuted else SetupTeal,
                    modifier = Modifier.padding(10.dp)
                )
            }
            Column(
                modifier = Modifier
                    .weight(1f)
                    .padding(horizontal = 12.dp)
            ) {
                Text(slotLabel, color = SetupNavy, fontWeight = FontWeight.Black, fontSize = 15.sp)
                Text(carrierLabel, color = SetupMuted, fontSize = 13.sp)
                if (paired) Text("مربوط بمحفظة", color = SetupTeal, fontSize = 12.sp, fontWeight = FontWeight.Bold)
            }
            RadioButton(selected = selected, onClick = null, enabled = !paired)
        }
    }
}

@Composable
private fun NoSubscriptionsNotice(onRefresh: () -> Unit) {
    Surface(
        color = Color.White,
        shape = RoundedCornerShape(18.dp),
        border = BorderStroke(1.dp, SetupLine),
        modifier = Modifier.fillMaxWidth()
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            Text("لم يتم العثور على خطوط فعالة", color = SetupNavy, fontWeight = FontWeight.Black)
            Text(
                "تأكد من تركيب الشريحة وتشغيلها، ثم أعد المحاولة.",
                color = SetupMuted,
                fontSize = 13.sp,
                modifier = Modifier.padding(top = 4.dp, bottom = 12.dp)
            )
            OutlinedButton(
                onClick = onRefresh,
                modifier = Modifier
                    .fillMaxWidth()
                    .heightIn(min = 48.dp)
            ) {
                Icon(Icons.Filled.Refresh, contentDescription = null)
                Spacer(Modifier.size(8.dp))
                Text("إعادة فحص الشرائح", fontWeight = FontWeight.Bold)
            }
        }
    }
}

private fun normalizePairingCode(enteredCode: String): String = enteredCode
    .filter(Char::isLetterOrDigit)
    .take(8)
    .uppercase(Locale.ROOT)

private suspend fun pairWallet(
    context: Context,
    preferences: PreferenceManager,
    attempt: PairingAttempt,
    gson: Gson
): PairingOutcome {
    if (preferences.hasWalletPairing(attempt.token)) {
        return PairingOutcome.Rejected("كود الربط مستخدم بالفعل على هذا الهاتف.")
    }
    if (SimSubscriptionMapping.isAssigned(attempt.subscription, preferences.getWalletPairings())) {
        return PairingOutcome.Rejected("هذا الخط مربوط بمحفظة أخرى بالفعل.")
    }

    val normalizedServerUrl = ApiClient.normalizeBaseUrl(attempt.serverUrl).trimEnd('/')
    preferences.saveServerUrl(normalizedServerUrl)
    val apiService = ApiClient.getApiService(context)
        ?: return PairingOutcome.Rejected("تعذر تجهيز اتصال المنصة.")

    return try {
        val response = withContext(Dispatchers.IO) {
            apiService.syncStatus(attempt.token, SyncStatusRequest(null))
        }
        val walletStatus = response.body()?.data
        if (!response.isSuccessful || response.body()?.success != true || walletStatus == null) {
            PairingOutcome.Rejected(apiFailureMessage(response.errorBody()?.string(), response.body()?.message, gson))
        } else {
            savePairing(preferences, attempt, walletStatus)
        }
    } catch (_: IOException) {
        PairingOutcome.Rejected("فشل الاتصال بالمنصة. تأكد من الإنترنت ثم حاول مرة أخرى.")
    } catch (_: JsonParseException) {
        PairingOutcome.Rejected("وصلت استجابة غير صالحة من المنصة. حاول مرة أخرى لاحقًا.")
    }
}

private fun savePairing(
    preferences: PreferenceManager,
    attempt: PairingAttempt,
    walletStatus: SyncStatusResponse
): PairingOutcome {
    val subscription = attempt.subscription
    val pairing = WalletPairing(
        token = attempt.token,
        subscriptionId = subscription.subscriptionId,
        simSlotIndex = subscription.slotIndex,
        simLabel = subscription.displayName.ifBlank { "SIM ${subscription.slotIndex + 1}" },
        carrierLabel = subscription.carrierName.ifBlank { subscription.displayName },
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

    return when (preferences.addWalletPairing(pairing)) {
        WalletPairingWriteResult.ADDED -> PairingOutcome.Saved
        WalletPairingWriteResult.SIM_CONFLICT -> PairingOutcome.Rejected("هذا الخط مربوط بمحفظة أخرى بالفعل.")
        WalletPairingWriteResult.TOKEN_CONFLICT,
        WalletPairingWriteResult.UNCHANGED -> PairingOutcome.Rejected("كود الربط مستخدم بالفعل على هذا الهاتف.")
        WalletPairingWriteResult.UPDATED,
        WalletPairingWriteResult.NOT_FOUND -> PairingOutcome.Rejected("تعذر حفظ ربط المحفظة. حاول مرة أخرى.")
    }
}

private fun apiFailureMessage(rawError: String?, responseMessage: String?, gson: Gson): String {
    if (!responseMessage.isNullOrBlank()) return responseMessage
    if (!rawError.isNullOrBlank()) {
        try {
            val parsedMessage = gson.fromJson(rawError, PairingApiError::class.java).message
            if (!parsedMessage.isNullOrBlank()) return parsedMessage
        } catch (_: JsonParseException) {
            // The fallback below remains user-safe when a proxy returns a non-JSON error page.
        }
    }
    return "كود الربط غير صالح أو المحفظة غير نشطة."
}
