package com.nadergorge.paymentlistener.ui.screens

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.nadergorge.paymentlistener.R
import com.nadergorge.paymentlistener.data.api.ApiClient
import com.nadergorge.paymentlistener.data.api.ApiResponse
import com.nadergorge.paymentlistener.data.api.SyncStatusRequest
import com.nadergorge.paymentlistener.data.preference.PreferenceManager
import com.google.gson.Gson
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SetupScreen(
    prefManager: PreferenceManager,
    onSetupSuccess: () -> Unit
) {
    val defaultServerUrl = stringResource(R.string.default_server_url)
    var pairingCode by remember { mutableStateOf("") }
    var isLoading by remember { mutableStateOf(false) }
    var errorMessage by remember { mutableStateOf<String?>(null) }
    val scope = rememberCoroutineScope()
    val context = LocalContext.current
    val gson = remember { Gson() }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(24.dp),
        verticalArrangement = Arrangement.Center,
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Text(
            text = "إعداد رابط ومستمع المدفوعات",
            fontSize = 24.sp,
            fontWeight = FontWeight.Bold,
            color = MaterialTheme.colorScheme.primary,
            textAlign = TextAlign.Center,
            modifier = Modifier.padding(bottom = 8.dp)
        )

        Text(
            text = "أدخل كود الربط من لوحة الإدارة لربط هذا الهاتف وتلقي ومزامنة الرسائل تلقائياً.",
            fontSize = 14.sp,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            textAlign = TextAlign.Center,
            modifier = Modifier.padding(bottom = 32.dp)
        )

        OutlinedTextField(
            value = pairingCode,
            onValueChange = { pairingCode = it.trim().uppercase() },
            label = { Text("كود الربط (Pairing Code)") },
            placeholder = { Text("كود مكون من 8 أرقام وحروف") },
            singleLine = true,
            keyboardOptions = KeyboardOptions(
                keyboardType = KeyboardType.Text,
                imeAction = ImeAction.Done
            ),
            modifier = Modifier
                .fillMaxWidth()
                .padding(bottom = 24.dp)
        )

        if (errorMessage != null) {
            Text(
                text = errorMessage!!,
                color = MaterialTheme.colorScheme.error,
                fontSize = 14.sp,
                fontWeight = FontWeight.Bold,
                textAlign = TextAlign.Center,
                modifier = Modifier.padding(bottom = 16.dp)
            )
        }

        Button(
            onClick = {
                if (pairingCode.isBlank()) {
                    errorMessage = "يرجى إدخال كود الربط."
                    return@Button
                }

                isLoading = true
                errorMessage = null

                scope.launch {
                    val normalizedServerUrl = ApiClient.normalizeBaseUrl(defaultServerUrl).trimEnd('/')
                    val normalizedPairingCode = pairingCode.trim().uppercase()

                    prefManager.saveServerUrl(normalizedServerUrl)
                    prefManager.savePairingToken(normalizedPairingCode)

                    val apiService = ApiClient.getApiService(context)
                    if (apiService == null) {
                        errorMessage = "تعذر تجهيز اتصال السيرفر."
                        isLoading = false
                        prefManager.clearConfiguration()
                        return@launch
                    }

                    try {
                        val response = withContext(Dispatchers.IO) {
                            apiService.syncStatus(normalizedPairingCode, SyncStatusRequest(null))
                        }

                        if (response.isSuccessful && response.body()?.success == true) {
                            val data = response.body()!!.data!!
                            prefManager.saveDevicePhone(data.phoneNumber)
                            prefManager.saveDeviceLabel(data.label)
                            prefManager.saveSmsFilters(data.smsSenderFilters)
                            prefManager.saveLastBalance(data.currentBalance.toFloat())
                            
                            onSetupSuccess()
                        } else {
                            val msg = response.body()?.message
                                ?: response.errorBody()?.string()?.let { raw ->
                                    runCatching {
                                        gson.fromJson(raw, ApiResponse::class.java).message
                                    }.getOrNull()
                                }
                                ?: "كود الربط غير صالح أو المحفظة غير نشطة."
                            errorMessage = msg
                            prefManager.clearConfiguration()
                        }
                    } catch (e: Exception) {
                        e.printStackTrace()
                        errorMessage = "فشل الاتصال بالسيرفر. تأكد من اتصال الإنترنت."
                        prefManager.clearConfiguration()
                    } finally {
                        isLoading = false
                    }
                }
            },
            enabled = !isLoading,
            modifier = Modifier
                .fillMaxWidth()
                .height(50.dp)
        ) {
            if (isLoading) {
                CircularProgressIndicator(
                    color = MaterialTheme.colorScheme.onPrimary,
                    modifier = Modifier.size(24.dp)
                )
            } else {
                Text(
                    text = "ربط وتفعيل الجهاز",
                    fontWeight = FontWeight.Bold,
                    fontSize = 16.sp
                )
            }
        }
    }
}
