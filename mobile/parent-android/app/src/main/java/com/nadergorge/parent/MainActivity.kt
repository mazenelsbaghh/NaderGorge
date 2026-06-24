package com.nadergorge.parent

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import com.nadergorge.parent.data.api.ParentApiService
import com.nadergorge.parent.data.repository.ParentRepository
import com.nadergorge.parent.data.storage.StorageService
import com.nadergorge.parent.ui.screens.DashboardScreen
import com.nadergorge.parent.ui.screens.DashboardViewModel
import com.nadergorge.parent.ui.screens.LinkingScreen
import com.nadergorge.parent.ui.screens.LinkingViewModel
import com.nadergorge.parent.ui.theme.ParentAppTheme
import okhttp3.OkHttpClient
import okhttp3.logging.HttpLoggingInterceptor
import retrofit2.Retrofit
import retrofit2.converter.gson.GsonConverterFactory

class MainActivity : ComponentActivity() {

    private lateinit var repository: ParentRepository
    private lateinit var linkingViewModel: LinkingViewModel
    private lateinit var dashboardViewModel: DashboardViewModel

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        // Initialize Services & Repository
        val storageService = StorageService(applicationContext)

        val logging = HttpLoggingInterceptor().apply {
            level = HttpLoggingInterceptor.Level.BODY
        }
        val client = OkHttpClient.Builder()
            .addInterceptor(logging)
            .build()

        val retrofit = Retrofit.Builder()
            .baseUrl("http://10.0.2.2:5245/") // standard Android Emulator localhost address
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
                    var currentScreen by remember { mutableStateOf("dashboard") }
                    val activeStudent by dashboardViewModel.activeStudent.collectAsState()

                    LaunchedEffect(activeStudent) {
                        if (activeStudent == null) {
                            currentScreen = "link"
                        } else {
                            currentScreen = "dashboard"
                        }
                    }

                    when (currentScreen) {
                        "link" -> {
                            LinkingScreen(
                                viewModel = linkingViewModel,
                                onSuccess = {
                                    dashboardViewModel.loadLinkedStudents()
                                    currentScreen = "dashboard"
                                }
                            )
                        }
                        "dashboard" -> {
                            DashboardScreen(
                                viewModel = dashboardViewModel,
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
