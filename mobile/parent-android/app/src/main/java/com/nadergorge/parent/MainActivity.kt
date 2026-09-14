package com.nadergorge.parent

import android.Manifest
import android.content.Intent
import android.graphics.Color
import android.net.Uri
import android.os.Build
import android.os.Bundle
import androidx.activity.compose.BackHandler
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Info
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.Alignment
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.core.view.WindowInsetsControllerCompat
import com.google.firebase.messaging.FirebaseMessaging
import com.nadergorge.parent.data.api.ParentAppConfigResponse
import com.nadergorge.parent.data.api.ParentApiService
import com.nadergorge.parent.data.repository.ParentRepository
import com.nadergorge.parent.data.storage.StorageService
import com.nadergorge.parent.service.NotificationHelper
import com.nadergorge.parent.service.ParentRefreshBus
import com.nadergorge.parent.ui.screens.DashboardScreen
import com.nadergorge.parent.ui.screens.DashboardViewModel
import com.nadergorge.parent.ui.screens.LinkingScreen
import com.nadergorge.parent.ui.screens.LinkingViewModel
import com.nadergorge.parent.ui.screens.OnboardingScreen
import com.nadergorge.parent.ui.screens.SplashScreen
import com.nadergorge.parent.ui.theme.ParentAppTheme
import okhttp3.OkHttpClient
import okhttp3.logging.HttpLoggingInterceptor
import retrofit2.Retrofit
import retrofit2.converter.gson.GsonConverterFactory
import kotlinx.coroutines.delay

class MainActivity : ComponentActivity() {

    private lateinit var repository: ParentRepository
    private lateinit var linkingViewModel: LinkingViewModel
    private lateinit var dashboardViewModel: DashboardViewModel

    override fun onResume() {
        super.onResume()
        if (::dashboardViewModel.isInitialized) {
            dashboardViewModel.refreshActiveStudent()
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        window.statusBarColor = Color.parseColor("#0E8F8F")
        window.navigationBarColor = Color.parseColor("#F6F7F8")
        WindowInsetsControllerCompat(window, window.decorView).apply {
            isAppearanceLightStatusBars = false
            isAppearanceLightNavigationBars = true
        }

        // Initialize Services & Repository
        val storageService = StorageService(applicationContext)

        val logging = HttpLoggingInterceptor().apply {
            level = HttpLoggingInterceptor.Level.BODY
        }
        val client = OkHttpClient.Builder()
            .addInterceptor(logging)
            .build()

        val retrofit = Retrofit.Builder()
            .baseUrl("https://api.massar-academy.net/") // massar production server URL
            .client(client)
            .addConverterFactory(GsonConverterFactory.create())
            .build()

        val apiService = retrofit.create(ParentApiService::class.java)
        repository = ParentRepository(apiService, storageService)

        linkingViewModel = LinkingViewModel(repository)
        dashboardViewModel = DashboardViewModel(repository)

        setContent {
            ParentAppTheme {
                Surface(
                    modifier = Modifier.fillMaxSize(),
                    color = MaterialTheme.colorScheme.background
                ) {
                    var currentScreen by remember { mutableStateOf("splash") }
                    var deviceToken by remember { mutableStateOf("android-parent-pending-token") }
                    var appConfig by remember { mutableStateOf<ParentAppConfigResponse?>(null) }
                    var notificationsEnabled by remember {
                        mutableStateOf(NotificationHelper.areNotificationsEnabled(this@MainActivity))
                    }
                    val activeStudent by dashboardViewModel.activeStudent.collectAsState()
                    val notificationPermissionLauncher = rememberLauncherForActivityResult(
                        ActivityResultContracts.RequestPermission()
                    ) {
                        notificationsEnabled = NotificationHelper.areNotificationsEnabled(this@MainActivity)
                    }

                    LaunchedEffect(Unit) {
                        NotificationHelper.ensureChannel(this@MainActivity)
                        repository.getAppConfig().onSuccess { config ->
                            appConfig = config
                        }
                        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU && !notificationsEnabled) {
                            notificationPermissionLauncher.launch(Manifest.permission.POST_NOTIFICATIONS)
                        }
                        FirebaseMessaging.getInstance().token.addOnSuccessListener { token ->
                            if (!token.isNullOrBlank()) {
                                deviceToken = token
                            }
                        }
                    }

                    LaunchedEffect(activeStudent) {
                        if (currentScreen == "splash") return@LaunchedEffect
                        if (activeStudent == null) {
                            if (currentScreen != "link") {
                                currentScreen = "onboarding"
                            }
                        } else {
                            currentScreen = "dashboard"
                        }
                    }

                    LaunchedEffect(activeStudent?.studentId, deviceToken) {
                        if (activeStudent != null && deviceToken != "android-parent-pending-token") {
                            dashboardViewModel.registerDeviceToken(deviceToken)
                        }
                    }

                    LaunchedEffect(activeStudent?.studentId) {
                        ParentRefreshBus.events.collect { changedStudentId ->
                            val currentStudentId = activeStudent?.studentId
                            if (changedStudentId.isNullOrBlank() || changedStudentId == currentStudentId) {
                                dashboardViewModel.refreshActiveStudent()
                            }
                        }
                    }

                    LaunchedEffect(activeStudent?.studentId, currentScreen) {
                        while (currentScreen == "dashboard" && activeStudent != null) {
                            delay(30_000)
                            dashboardViewModel.refreshActiveStudent()
                        }
                    }

                    BackHandler(enabled = currentScreen != "splash" && currentScreen != "dashboard") {
                        currentScreen = when (currentScreen) {
                            "link" -> if (activeStudent != null) "dashboard" else "onboarding"
                            "onboarding" -> if (activeStudent != null) "dashboard" else "onboarding"
                            else -> currentScreen
                        }
                    }

                    if (appConfig?.updateRequired == true) {
                        ParentUpdateRequiredScreen(
                            message = appConfig?.updateMessage.orEmpty(),
                            updateUrl = appConfig?.updateUrl.orEmpty(),
                            onUpdate = { url ->
                                if (url.isNotBlank()) {
                                    startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(url)))
                                }
                            }
                        )
                    } else when (currentScreen) {
                        "splash" -> {
                            SplashScreen(
                                onFinished = {
                                    currentScreen = if (activeStudent == null) "onboarding" else "dashboard"
                                }
                            )
                        }
                        "onboarding" -> {
                            OnboardingScreen(
                                onStartTracking = {
                                    currentScreen = "link"
                                }
                            )
                        }
                        "link" -> {
                            LinkingScreen(
                                viewModel = linkingViewModel,
                                deviceToken = deviceToken,
                                onSuccess = {
                                    dashboardViewModel.loadLinkedStudents()
                                    currentScreen = "dashboard"
                                },
                                onBack = {
                                    if (activeStudent != null) {
                                        currentScreen = "dashboard"
                                    } else {
                                        currentScreen = "onboarding"
                                    }
                                }
                            )
                        }
                        "dashboard" -> {
                            DashboardScreen(
                                viewModel = dashboardViewModel,
                                notificationsEnabled = notificationsEnabled,
                                onTestNotification = {
                                    if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU &&
                                        !NotificationHelper.areNotificationsEnabled(this@MainActivity)
                                    ) {
                                        notificationPermissionLauncher.launch(Manifest.permission.POST_NOTIFICATIONS)
                                    } else {
                                        NotificationHelper.showLocalNotification(
                                            this@MainActivity,
                                            "اختبار الإشعارات",
                                            "الإشعارات تعمل داخل تطبيق ولي الأمر."
                                        )
                                    }
                                    notificationsEnabled = NotificationHelper.areNotificationsEnabled(this@MainActivity)
                                },
                                onNavigateToLink = {
                                    currentScreen = "link"
                                }
                            )
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun ParentUpdateRequiredScreen(
    message: String,
    updateUrl: String,
    onUpdate: (String) -> Unit
) {
    Surface(
        modifier = Modifier.fillMaxSize(),
        color = MaterialTheme.colorScheme.background
    ) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(28.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.Center
        ) {
            androidx.compose.material3.Icon(
                imageVector = Icons.Default.Info,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.primary,
                modifier = Modifier.size(56.dp)
            )
            Spacer(modifier = Modifier.height(18.dp))
            Text(
                text = "تحديث مطلوب",
                style = MaterialTheme.typography.headlineSmall.copy(fontWeight = FontWeight.Black),
                color = MaterialTheme.colorScheme.onSurface,
                textAlign = TextAlign.Center
            )
            Spacer(modifier = Modifier.height(10.dp))
            Text(
                text = message.ifBlank { "يوجد تحديث جديد لتطبيق ولي الأمر. برجاء تحديث التطبيق للمتابعة." },
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurface.copy(alpha = 0.75f),
                textAlign = TextAlign.Center
            )
            Spacer(modifier = Modifier.height(24.dp))
            Button(
                onClick = { onUpdate(updateUrl) },
                enabled = updateUrl.isNotBlank(),
                modifier = Modifier
                    .fillMaxWidth()
                    .height(52.dp)
            ) {
                Text("اذهب للتحديث", fontWeight = FontWeight.Bold)
            }
        }
    }
}
