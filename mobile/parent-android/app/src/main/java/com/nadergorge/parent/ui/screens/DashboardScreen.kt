package com.nadergorge.parent.ui.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.graphics.Brush
import androidx.compose.foundation.BorderStroke
import com.nadergorge.parent.data.api.ExamInfo
import com.nadergorge.parent.data.api.HomeworkInfo
import com.nadergorge.parent.data.api.StudentDetailsResponse
import com.nadergorge.parent.data.api.WarningInfo
import com.nadergorge.parent.ui.theme.*

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DashboardScreen(
    viewModel: DashboardViewModel,
    onNavigateToLink: () -> Unit,
    modifier: Modifier = Modifier
) {
    val uiState by viewModel.uiState.collectAsState()
    val linkedStudents by viewModel.linkedStudents.collectAsState()
    val activeStudent by viewModel.activeStudent.collectAsState()

    var dropdownExpanded by remember { mutableStateOf(false) }
    
    // Navigation tabs inside Dashboard
    var activeTab by remember { mutableStateOf("home") } // "home", "schedule", "homework", "grades", "more"
    var activeSubScreen by remember { mutableStateOf<String?>(null) } // "profile", "attendance", "notes", "fees", "notifications", "settings"

    if (activeSubScreen != null) {
        val details = (uiState as? DashboardUiState.Success)?.details
        val name = details?.studentName ?: activeStudent?.name ?: "طالب مسار"
        val grade = details?.grade ?: "الصف الدراسي"
        val school = details?.school ?: "مدرسة مسار"

        when (activeSubScreen) {
            "profile" -> ProfileScreen(
                studentName = name,
                grade = grade,
                school = school,
                onBack = { activeSubScreen = null }
            )
            "attendance" -> AttendanceScreen(
                watchedLessons = details?.attendance?.watchedLessons ?: 18,
                totalLessons = details?.attendance?.totalLessons ?: 20,
                completionRate = details?.attendance?.completionRate ?: 90.0,
                onBack = { activeSubScreen = null }
            )
            "grades" -> GradesScreen(
                exams = details?.exams ?: emptyList(),
                onBack = { activeSubScreen = null }
            )
            "homework" -> HomeworkScreen(
                homeworks = details?.homeworks ?: emptyList(),
                onBack = { activeSubScreen = null }
            )
            "notes" -> NotesScreen(
                onBack = { activeSubScreen = null }
            )
            "fees" -> FeesScreen(
                onBack = { activeSubScreen = null }
            )
            "notifications" -> NotificationsScreen(
                warnings = details?.warnings ?: emptyList(),
                onBack = { activeSubScreen = null }
            )
            "settings" -> SettingsScreen(
                onBack = { activeSubScreen = null },
                onLogout = {
                    activeSubScreen = null
                    activeStudent?.let { viewModel.removeStudent(it.studentId) }
                }
            )
        }
    } else {
        val unselectedTabColor = if (isSystemInDarkTheme()) TextSecondary else LightTextSecondary
        Scaffold(
            topBar = {
                TopAppBar(
                    title = {
                        Row(
                            verticalAlignment = Alignment.CenterVertically,
                            modifier = Modifier.fillMaxWidth()
                        ) {
                            MassarLogo(showText = false)
                            Spacer(modifier = Modifier.width(8.dp))
                            Text(
                                text = activeStudent?.name ?: "منصة مسار",
                                style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Black),
                                color = MaterialTheme.colorScheme.onSurface,
                                modifier = Modifier.weight(1f)
                            )
                            IconButton(onClick = { dropdownExpanded = true }) {
                                Icon(Icons.Default.ArrowDropDown, contentDescription = "تبديل الطالب", tint = MaterialTheme.colorScheme.onSurface)
                            }
                            DropdownMenu(
                                expanded = dropdownExpanded,
                                onDismissRequest = { dropdownExpanded = false }
                            ) {
                                linkedStudents.forEach { student ->
                                    DropdownMenuItem(
                                        text = { Text(student.name, fontWeight = FontWeight.Bold) },
                                        onClick = {
                                            viewModel.switchActiveStudent(student.studentId)
                                            dropdownExpanded = false
                                        }
                                    )
                                }
                                HorizontalDivider()
                                DropdownMenuItem(
                                    text = {
                                        Row(verticalAlignment = Alignment.CenterVertically) {
                                            Icon(Icons.Default.Add, contentDescription = null, modifier = Modifier.size(16.dp))
                                            Spacer(modifier = Modifier.width(8.dp))
                                            Text("ربط طالب جديد")
                                        }
                                    },
                                    onClick = {
                                        dropdownExpanded = false
                                        onNavigateToLink()
                                    }
                                )
                            }
                        }
                    },
                    actions = {
                        IconButton(onClick = { activeSubScreen = "notifications" }) {
                            Icon(Icons.Default.Notifications, contentDescription = "التنبيهات", tint = MaterialTheme.colorScheme.onSurface)
                        }
                    },
                    colors = TopAppBarDefaults.topAppBarColors(containerColor = MaterialTheme.colorScheme.surface)
                )
            }
        ) { innerPadding ->
            Box(
                modifier = modifier
                    .fillMaxSize()
                    .padding(innerPadding)
                    .background(MaterialTheme.colorScheme.background)
            ) {
                // Main tab content
                when (val state = uiState) {
                    is DashboardUiState.Empty -> {
                        Box(modifier = Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                            Column(horizontalAlignment = Alignment.CenterHorizontally) {
                                Text(
                                    "لم يتم ربط أي طالب بعد",
                                    style = MaterialTheme.typography.titleMedium,
                                    color = TextSecondary,
                                    modifier = Modifier.padding(bottom = 16.dp)
                                )
                                Button(onClick = onNavigateToLink) {
                                    Text("ربط طالب الآن")
                                }
                            }
                        }
                    }
                    is DashboardUiState.Loading -> {
                        Box(modifier = Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                            CircularProgressIndicator(color = BrandTeal)
                        }
                    }
                    is DashboardUiState.Success -> {
                        when (activeTab) {
                            "home" -> HomeTabView(
                                details = state.details,
                                onNavigateToSub = { activeSubScreen = it }
                            )
                            "schedule" -> ScheduleScreen(onBack = { activeTab = "home" })
                            "homework" -> HomeworkScreen(homeworks = state.details.homeworks, onBack = { activeTab = "home" })
                            "grades" -> GradesScreen(exams = state.details.exams, onBack = { activeTab = "home" })
                            "more" -> MoreMenuView(onSelectSub = { activeSubScreen = it })
                        }
                    }
                    is DashboardUiState.Error -> {
                        Box(modifier = Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                            Column(
                                horizontalAlignment = Alignment.CenterHorizontally,
                                modifier = Modifier.padding(24.dp)
                            ) {
                                Text(
                                    state.message,
                                    style = MaterialTheme.typography.bodyLarge,
                                    color = MaterialTheme.colorScheme.error,
                                    textAlign = TextAlign.Center,
                                    modifier = Modifier.padding(bottom = 16.dp)
                                )
                                Button(onClick = { activeStudent?.let { viewModel.fetchStudentDetails(it.studentId) } }) {
                                    Text("إعادة المحاولة")
                                }
                            }
                        }
                    }
                }

                // Floating Navigation Bar (Aligned to bottom center)
                if (uiState is DashboardUiState.Success) {
                    Box(
                        modifier = Modifier
                            .fillMaxWidth()
                            .align(Alignment.BottomCenter)
                            .padding(horizontal = 16.dp, vertical = 16.dp)
                    ) {
                        Card(
                            shape = RoundedCornerShape(24.dp),
                            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                            elevation = CardDefaults.cardElevation(defaultElevation = 8.dp),
                            border = BorderStroke(1.dp, MaterialTheme.colorScheme.onSurface.copy(alpha = 0.08f)),
                            modifier = Modifier.fillMaxWidth()
                        ) {
                            Row(
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .height(64.dp)
                                    .padding(horizontal = 4.dp),
                                horizontalArrangement = Arrangement.SpaceAround,
                                verticalAlignment = Alignment.CenterVertically
                            ) {
                                val tabs = listOf(
                                    Triple("home", Icons.Default.Home, "الرئيسية"),
                                    Triple("schedule", Icons.Default.DateRange, "الجدول"),
                                    Triple("homework", Icons.Default.Edit, "الواجبات"),
                                    Triple("grades", Icons.Default.Star, "الدرجات"),
                                    Triple("more", Icons.Default.Menu, "المزيد")
                                )
                                tabs.forEach { (tabId, icon, label) ->
                                    val isSelected = activeTab == tabId
                                    Column(
                                        modifier = Modifier
                                            .weight(1f)
                                            .fillMaxHeight()
                                            .clickable(
                                                interactionSource = remember { androidx.compose.foundation.interaction.MutableInteractionSource() },
                                                indication = null
                                            ) { activeTab = tabId },
                                        horizontalAlignment = Alignment.CenterHorizontally,
                                        verticalArrangement = Arrangement.SpaceBetween
                                    ) {
                                        // Top Indicator
                                        Box(
                                            modifier = Modifier
                                                .width(32.dp)
                                                .height(3.dp)
                                                .clip(RoundedCornerShape(bottomStart = 2.dp, bottomEnd = 2.dp))
                                                .background(if (isSelected) BrandTeal else Color.Transparent)
                                        )
                                        
                                        Icon(
                                            imageVector = icon,
                                            contentDescription = label,
                                            tint = if (isSelected) BrandTeal else unselectedTabColor,
                                            modifier = Modifier.size(20.dp)
                                        )
                                        
                                        Text(
                                            text = label,
                                            fontSize = 10.sp,
                                            fontWeight = if (isSelected) FontWeight.Bold else FontWeight.Medium,
                                            color = if (isSelected) BrandTeal else unselectedTabColor,
                                            modifier = Modifier.padding(bottom = 6.dp)
                                        )
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}

// --- Tab 1: Home View (Reworked to use the grid widget layout of Screen 3) ---
@Composable
fun HomeTabView(
    details: StudentDetailsResponse,
    onNavigateToSub: (String) -> Unit
) {
    LazyColumn(
        modifier = Modifier
            .fillMaxSize()
            .padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp)
    ) {
        // Welcome Card (Screen 3 Header visual representation)
        item {
            Card(
                shape = RoundedCornerShape(16.dp),
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                modifier = Modifier.fillMaxWidth()
            ) {
                Row(
                    modifier = Modifier
                        .padding(16.dp)
                        .fillMaxWidth(),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Box(
                        modifier = Modifier
                            .size(56.dp)
                            .clip(CircleShape)
                            .background(BrandDeepNavy),
                        contentAlignment = Alignment.Center
                    ) {
                        Text(
                            text = details.studentName.take(1),
                            fontSize = 22.sp,
                            fontWeight = FontWeight.Black,
                            color = Color.White
                        )
                    }
                    Spacer(modifier = Modifier.width(16.dp))
                    Column {
                        Text(
                            text = "مرحباً ولي أمر",
                            fontSize = 12.sp,
                            color = TextSecondary,
                            fontWeight = FontWeight.SemiBold
                        )
                        Text(
                            text = details.studentName,
                            fontSize = 16.sp,
                            fontWeight = FontWeight.Bold,
                            color = MaterialTheme.colorScheme.onSurface
                        )
                        Text(
                            text = "${details.grade} • ${details.school}",
                            fontSize = 11.sp,
                            color = TextSecondary,
                            modifier = Modifier.padding(top = 2.dp)
                        )
                    }
                }
            }
        }

        // Metrics Grid (Screen 3 dashboard widgets)
        item {
            Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                Row(horizontalArrangement = Arrangement.spacedBy(12.dp), modifier = Modifier.fillMaxWidth()) {
                    MetricWidgetCard(
                        title = "الحضور",
                        value = "${details.attendance.completionRate.toInt()}%",
                        sub = "نسبة الالتزام",
                        icon = Icons.Default.CheckCircle,
                        color = BrandTeal,
                        modifier = Modifier.weight(1f)
                    ) {
                        onNavigateToSub("attendance")
                    }
                    MetricWidgetCard(
                        title = "آخر درجة",
                        value = "88",
                        sub = "ممتاز",
                        icon = Icons.Default.Star,
                        color = BrandWarmGold,
                        modifier = Modifier.weight(1f)
                    ) {
                        onNavigateToSub("grades")
                    }
                }
                Row(horizontalArrangement = Arrangement.spacedBy(12.dp), modifier = Modifier.fillMaxWidth()) {
                    MetricWidgetCard(
                        title = "الواجبات",
                        value = "${details.homeworks.filter { !it.isSubmitted }.size}",
                        sub = "واجبات متبقية",
                        icon = Icons.Default.Edit,
                        color = if (isSystemInDarkTheme()) BrandTeal else BrandDeepNavy,
                        modifier = Modifier.weight(1f)
                    ) {
                        onNavigateToSub("homework")
                    }
                    MetricWidgetCard(
                        title = "الإنذارات",
                        value = "${details.warnings.size}",
                        sub = "تنبيه إرشادي",
                        icon = Icons.Default.Warning,
                        color = WarningHigh,
                        modifier = Modifier.weight(1f)
                    ) {
                        onNavigateToSub("notifications")
                    }
                }
            }
        }

        // "تنبيه مهم" Banner Card
        item {
            Card(
                shape = RoundedCornerShape(12.dp),
                colors = CardDefaults.cardColors(containerColor = BrandWarmGold.copy(alpha = if (isSystemInDarkTheme()) 0.08f else 0.12f)),
                border = BorderStroke(1.dp, BrandWarmGold.copy(alpha = 0.2f)),
                modifier = Modifier.fillMaxWidth()
            ) {
                Row(
                    modifier = Modifier.padding(16.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Box(
                        modifier = Modifier
                            .size(36.dp)
                            .clip(CircleShape)
                            .background(BrandWarmGold.copy(alpha = 0.15f)),
                        contentAlignment = Alignment.Center
                    ) {
                        Icon(Icons.Default.Warning, contentDescription = null, tint = BrandWarmGold, modifier = Modifier.size(18.dp))
                    }
                    Spacer(modifier = Modifier.width(12.dp))
                    Column {
                        Text("تنبيه مهم جداً", fontWeight = FontWeight.Bold, fontSize = 13.sp, color = MaterialTheme.colorScheme.onSurface)
                        Text(
                            "موعد اختبار الرياضيات الشامل يوم الأحد القادم 12 مايو.",
                            fontSize = 12.sp,
                            color = TextSecondary,
                            modifier = Modifier.padding(top = 2.dp)
                        )
                    }
                }
            }
        }

        // Latest Notifications Section ("آخر التنبيهات")
        item {
            Column(verticalArrangement = Arrangement.spacedBy(12.dp), modifier = Modifier.padding(top = 8.dp)) {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Text(
                        text = "آخر التنبيهات",
                        fontSize = 16.sp,
                        fontWeight = FontWeight.Black,
                        color = MaterialTheme.colorScheme.onSurface
                    )
                    Text(
                        text = "عرض الكل",
                        fontSize = 12.sp,
                        fontWeight = FontWeight.Bold,
                        color = BrandTeal,
                        modifier = Modifier.clickable { onNavigateToSub("notifications") }
                    )
                }

                // Item 1: grading/Arabic
                Card(
                    shape = RoundedCornerShape(12.dp),
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Row(
                        modifier = Modifier.padding(16.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Box(
                            modifier = Modifier
                                .size(40.dp)
                                .clip(CircleShape)
                                .background(BrandTeal.copy(alpha = 0.1f)),
                            contentAlignment = Alignment.Center
                        ) {
                            Icon(Icons.Default.Star, contentDescription = null, tint = BrandTeal, modifier = Modifier.size(18.dp))
                        }
                        Spacer(modifier = Modifier.width(12.dp))
                        Column {
                            Text("تم رصد درجة اختبار اللغة العربية", fontSize = 13.sp, fontWeight = FontWeight.Medium, color = MaterialTheme.colorScheme.onSurface)
                            Text("منذ ساعتين", fontSize = 11.sp, color = Color.Gray)
                        }
                    }
                }

                // Item 2: absence/person_off
                Card(
                    shape = RoundedCornerShape(12.dp),
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Row(
                        modifier = Modifier.padding(16.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Box(
                            modifier = Modifier
                                .size(40.dp)
                                .clip(CircleShape)
                                .background(WarningHigh.copy(alpha = 0.1f)),
                            contentAlignment = Alignment.Center
                        ) {
                            Icon(Icons.Default.Close, contentDescription = null, tint = WarningHigh, modifier = Modifier.size(18.dp))
                        }
                        Spacer(modifier = Modifier.width(12.dp))
                        Column {
                            Text("تسجيل غياب في الحصة الثالثة", fontSize = 13.sp, fontWeight = FontWeight.Medium, color = MaterialTheme.colorScheme.onSurface)
                            Text("أمس", fontSize = 11.sp, color = Color.Gray)
                        }
                    }
                }
            }
        }

        // General Academic Progress Visual Card
        item {
            Card(
                shape = RoundedCornerShape(16.dp),
                colors = CardDefaults.cardColors(containerColor = BrandTeal),
                modifier = Modifier.fillMaxWidth()
            ) {
                Column(
                    modifier = Modifier
                        .background(
                            Brush.linearGradient(
                                colors = listOf(BrandTeal, BrandTeal.copy(alpha = 0.85f))
                            )
                        )
                        .padding(20.dp)
                ) {
                    Text("التقدم الأكاديمي العام", fontSize = 12.sp, color = Color.White.copy(alpha = 0.8f))
                    Spacer(modifier = Modifier.height(4.dp))
                    Text("أداء متميز هذا الفصل", fontSize = 18.sp, fontWeight = FontWeight.Black, color = Color.White)
                    Spacer(modifier = Modifier.height(16.dp))

                    LinearProgressIndicator(
                        progress = 0.75f,
                        color = Color.White,
                        trackColor = Color.White.copy(alpha = 0.2f),
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(8.dp)
                            .clip(CircleShape)
                    )
                    Spacer(modifier = Modifier.height(6.dp))
                    Text(
                        text = "75% مكتمل",
                        fontSize = 11.sp,
                        color = Color.White.copy(alpha = 0.8f),
                        modifier = Modifier.fillMaxWidth(),
                        textAlign = TextAlign.End
                    )
                }
            }
        }
        
        // Spacer for bottom navigation bar overlay safety
        item {
            Spacer(modifier = Modifier.height(80.dp))
        }
    }
}

// Custom Grid metric card
@Composable
fun MetricWidgetCard(
    title: String,
    value: String,
    sub: String,
    icon: ImageVector,
    color: Color,
    modifier: Modifier = Modifier,
    onClick: () -> Unit
) {
    Card(
        shape = RoundedCornerShape(16.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        modifier = modifier.clickable { onClick() }
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(title, fontWeight = FontWeight.Bold, fontSize = 13.sp, color = TextSecondary)
                Icon(icon, contentDescription = null, tint = color, modifier = Modifier.size(20.dp))
            }
            Spacer(modifier = Modifier.height(12.dp))
            Text(value, fontWeight = FontWeight.Black, fontSize = 28.sp, color = MaterialTheme.colorScheme.onSurface)
            Text(sub, fontSize = 11.sp, color = color, fontWeight = FontWeight.Bold)
        }
    }
}

// --- Tab 5: More Menu View ---
@Composable
fun MoreMenuView(
    onSelectSub: (String) -> Unit
) {
    LazyColumn(
        modifier = Modifier
            .fillMaxSize()
            .padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        item { MoreMenuRow(title = "الملف الشخصي للطالب", icon = Icons.Default.Person) { onSelectSub("profile") } }
        item { MoreMenuRow(title = "سجل الغياب والحضور", icon = Icons.Default.CheckCircle) { onSelectSub("attendance") } }
        item { MoreMenuRow(title = "ملاحظات المدرسين", icon = Icons.Default.Info) { onSelectSub("notes") } }
        item { MoreMenuRow(title = "المصاريف والمدفوعات", icon = Icons.Default.ShoppingCart) { onSelectSub("fees") } }
        item { MoreMenuRow(title = "إعدادات التطبيق", icon = Icons.Default.Settings) { onSelectSub("settings") } }
    }
}

@Composable
fun MoreMenuRow(title: String, icon: ImageVector, onClick: () -> Unit) {
    Card(
        shape = RoundedCornerShape(12.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        modifier = Modifier
            .fillMaxWidth()
            .clickable { onClick() }
    ) {
        Row(
            modifier = Modifier.padding(16.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Icon(icon, contentDescription = null, tint = BrandTeal, modifier = Modifier.size(22.dp))
            Spacer(modifier = Modifier.width(16.dp))
            Text(title, fontWeight = FontWeight.Bold, fontSize = 14.sp, color = MaterialTheme.colorScheme.onSurface, modifier = Modifier.weight(1f))
            Icon(Icons.Default.PlayArrow, contentDescription = null, tint = TextSecondary.copy(alpha = 0.4f), modifier = Modifier.size(16.dp))
        }
    }
}
