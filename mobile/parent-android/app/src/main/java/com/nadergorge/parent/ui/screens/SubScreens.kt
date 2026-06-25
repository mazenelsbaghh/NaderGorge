package com.nadergorge.parent.ui.screens

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowLeft
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowRight
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.foundation.Image
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.nadergorge.parent.data.api.ExamInfo
import com.nadergorge.parent.data.api.HomeworkInfo
import com.nadergorge.parent.data.api.StudentDetailsResponse
import com.nadergorge.parent.data.api.WarningInfo
import com.nadergorge.parent.ui.theme.*

// --- Custom Massar Logo Component ---
@Composable
fun MassarLogo(
    modifier: Modifier = Modifier,
    showText: Boolean = true,
    isDarkBg: Boolean = false
) {
    Row(
        modifier = modifier,
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.Center
    ) {
        // Draw the Stairs Logo Mark
        Image(
            painter = painterResource(
                id = if (isDarkBg) com.nadergorge.parent.R.drawable.ic_logo_mark_light else com.nadergorge.parent.R.drawable.ic_logo_mark
            ),
            contentDescription = null,
            modifier = Modifier.size(36.dp)
        )

        if (showText) {
            Spacer(modifier = Modifier.width(8.dp))
            Column(horizontalAlignment = Alignment.Start) {
                Text(
                    text = "مسار",
                    fontSize = 18.sp,
                    fontWeight = FontWeight.Black,
                    color = if (isDarkBg) Color.White else BrandDeepNavy
                )
                Text(
                    text = "أكاديمي",
                    fontSize = 11.sp,
                    fontWeight = FontWeight.Bold,
                    color = BrandTeal
                )
            }
        }
    }
}

// --- Screen 4: Profile Screen ---
@Composable
fun ProfileScreen(
    studentName: String,
    grade: String,
    school: String,
    onBack: () -> Unit
) {
    Scaffold(
        topBar = {
            ProfileTopBar(title = "الملف الشخصي", onBack = onBack)
        }
    ) { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .background(MaterialTheme.colorScheme.background)
                .padding(16.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            // Profile Card
            Card(
                shape = RoundedCornerShape(16.dp),
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                modifier = Modifier.fillMaxWidth().padding(bottom = 24.dp)
            ) {
                Column(
                    modifier = Modifier.padding(24.dp),
                    horizontalAlignment = Alignment.CenterHorizontally
                ) {
                    Box(
                        modifier = Modifier
                            .size(80.dp)
                            .clip(CircleShape)
                            .background(BrandDeepNavy),
                        contentAlignment = Alignment.Center
                    ) {
                        Text(
                            text = studentName.take(1),
                            fontSize = 32.sp,
                            fontWeight = FontWeight.Black,
                            color = Color.White
                        )
                    }
                    Spacer(modifier = Modifier.height(16.dp))
                    Text(
                        text = studentName,
                        fontSize = 20.sp,
                        fontWeight = FontWeight.Bold,
                        color = MaterialTheme.colorScheme.onSurface
                    )
                    Spacer(modifier = Modifier.height(4.dp))
                    Text(
                        text = "رقم المتابعة: MSR-2026-00125",
                        fontSize = 13.sp,
                        color = BrandTeal,
                        fontWeight = FontWeight.SemiBold
                    )
                }
            }

            // Info Details
            Column(
                modifier = Modifier.fillMaxWidth(),
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                InfoRow(label = "الصف الدراسي", value = grade, icon = Icons.Default.Home)
                InfoRow(label = "المجموعة الدراسية", value = "مجموعة A", icon = Icons.Default.Star)
                InfoRow(label = "تاريخ الميلاد", value = "15/05/2009", icon = Icons.Default.DateRange)
                InfoRow(label = "عدد المدرسين والمشرفين", value = "6 مدرسين", icon = Icons.Default.Person)
            }

            Spacer(modifier = Modifier.weight(1f))

            Button(
                onClick = {},
                colors = ButtonDefaults.buttonColors(containerColor = if (isSystemInDarkTheme()) BrandTeal else BrandDeepNavy),
                modifier = Modifier.fillMaxWidth().height(50.dp).clip(RoundedCornerShape(12.dp))
            ) {
                Text("عرض تفاصيل التسجيل الأكاديمي", color = Color.White)
            }
        }
    }
}

// --- Screen 5: Attendance Screen ---
@Composable
fun AttendanceScreen(
    watchedLessons: Int,
    totalLessons: Int,
    completionRate: Double,
    onBack: () -> Unit
) {
    Scaffold(
        topBar = {
            ProfileTopBar(title = "الحضور والغياب", onBack = onBack)
        }
    ) { padding ->
        LazyColumn(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .background(MaterialTheme.colorScheme.background)
                .padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(16.dp)
        ) {
            // Calendar Picker Placeholder View
            item {
                Card(
                    shape = RoundedCornerShape(16.dp),
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Column(modifier = Modifier.padding(16.dp)) {
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.SpaceBetween,
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Icon(Icons.AutoMirrored.Default.KeyboardArrowRight, contentDescription = null)
                            Text("مايو 2026", fontWeight = FontWeight.Bold, fontSize = 16.sp)
                            Icon(Icons.AutoMirrored.Default.KeyboardArrowLeft, contentDescription = null)
                        }
                        Spacer(modifier = Modifier.height(12.dp))
                        // Draw a simple calendar grid representation
                        CalendarGrid()
                    }
                }
            }

            // Stats Gauge Widget
            item {
                Card(
                    shape = RoundedCornerShape(16.dp),
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Column(modifier = Modifier.padding(16.dp)) {
                        Text(
                            text = "نسبة الالتزام الإجمالية",
                            fontWeight = FontWeight.Bold,
                            fontSize = 15.sp,
                            modifier = Modifier.padding(bottom = 12.dp)
                        )
                        Row(
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.spacedBy(24.dp)
                        ) {
                            Box(contentAlignment = Alignment.Center, modifier = Modifier.size(80.dp)) {
                                CircularProgressIndicator(
                                    progress = (completionRate / 100).toFloat(),
                                    color = BrandTeal,
                                    strokeWidth = 8.dp,
                                    modifier = Modifier.fillMaxSize()
                                )
                                Text("${completionRate.toInt()}%", fontWeight = FontWeight.Black, fontSize = 16.sp)
                            }
                            Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
                                StatusCountRow(label = "حاضر ومكتمل", count = watchedLessons, color = PassGreen)
                                StatusCountRow(label = "غائب ومتأخر", count = totalLessons - watchedLessons, color = FailRed)
                                StatusCountRow(label = "إجازة رسمية", count = 2, color = Color.Gray)
                            }
                        }
                    }
                }
            }
        }
    }
}

// --- Screen 6: Grades Screen ---
@Composable
fun GradesScreen(
    exams: List<ExamInfo>,
    onBack: () -> Unit
) {
    var selectedTab by remember { mutableStateOf(0) }

    Scaffold(
        topBar = {
            ProfileTopBar(title = "الدرجات والتقييمات", onBack = onBack)
        }
    ) { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .background(MaterialTheme.colorScheme.background)
        ) {
            // Tab Switcher
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(16.dp)
                    .clip(RoundedCornerShape(8.dp))
                    .background(MaterialTheme.colorScheme.surface)
            ) {
                TabButton(text = "الاختبارات", selected = selectedTab == 0, modifier = Modifier.weight(1f)) { selectedTab = 0 }
                TabButton(text = "المواد الدراسية", selected = selectedTab == 1, modifier = Modifier.weight(1f)) { selectedTab = 1 }
            }

            LazyColumn(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(horizontal = 16.dp),
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                if (selectedTab == 0) {
                    // Line Chart Representation Card
                    item {
                        Card(
                            shape = RoundedCornerShape(16.dp),
                            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                            modifier = Modifier.fillMaxWidth()
                        ) {
                            Column(modifier = Modifier.padding(16.dp)) {
                                Text("متوسط درجات الطالب", fontWeight = FontWeight.Bold, fontSize = 14.sp)
                                Spacer(modifier = Modifier.height(4.dp))
                                Text("88% (ممتاز)", color = BrandTeal, fontWeight = FontWeight.Black, fontSize = 20.sp)
                                Spacer(modifier = Modifier.height(16.dp))
                                LineChartVisual()
                            }
                        }
                    }

                    items(exams) { exam ->
                        ExamItem(exam)
                    }
                } else {
                    item {
                        SubjectGradesList()
                    }
                }
            }
        }
    }
}

// --- Screen 7: Homework Screen ---
@Composable
fun HomeworkScreen(
    homeworks: List<HomeworkInfo>,
    onBack: () -> Unit
) {
    var filterTab by remember { mutableStateOf(0) } // 0: All, 1: Pending, 2: Submitted

    Scaffold(
        topBar = {
            ProfileTopBar(title = "الواجبات المنزلية", onBack = onBack)
        }
    ) { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .background(MaterialTheme.colorScheme.background)
        ) {
            // Three-Way Filter Tabs
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(16.dp)
                    .clip(RoundedCornerShape(8.dp))
                    .background(MaterialTheme.colorScheme.surface)
            ) {
                TabButton(text = "الكل", selected = filterTab == 0, modifier = Modifier.weight(1f)) { filterTab = 0 }
                TabButton(text = "متبقي", selected = filterTab == 1, modifier = Modifier.weight(1f)) { filterTab = 1 }
                TabButton(text = "تم تسليمه", selected = filterTab == 2, modifier = Modifier.weight(1f)) { filterTab = 2 }
            }

            val filteredList = when (filterTab) {
                1 -> homeworks.filter { !it.isSubmitted }
                2 -> homeworks.filter { it.isSubmitted }
                else -> homeworks
            }

            LazyColumn(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(horizontal = 16.dp),
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                if (filteredList.isEmpty()) {
                    item {
                        Box(modifier = Modifier.fillMaxWidth().padding(48.dp), contentAlignment = Alignment.Center) {
                            Text("لا يوجد واجبات في هذا التبويب", color = TextSecondary)
                        }
                    }
                } else {
                    items(filteredList) { hw ->
                        HomeworkItem(hw)
                    }
                }
            }
        }
    }
}

// --- Screen 8: Class Schedule Screen ---
@Composable
fun ScheduleScreen(
    onBack: () -> Unit
) {
    val days = listOf("السبت", "الأحد", "الأثنين", "الثلاثاء", "الأربعاء")
    var selectedDay by remember { mutableStateOf(0) }

    Scaffold(
        topBar = {
            ProfileTopBar(title = "الجدول الدراسي", onBack = onBack)
        }
    ) { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .background(MaterialTheme.colorScheme.background)
        ) {
            // Day selector tabs
            ScrollableTabRow(
                selectedTabIndex = selectedDay,
                edgePadding = 16.dp,
                containerColor = MaterialTheme.colorScheme.surface,
                contentColor = BrandTeal
            ) {
                days.forEachIndexed { index, day ->
                    Tab(
                        selected = selectedDay == index,
                        onClick = { selectedDay = index },
                        text = { Text(day, fontWeight = FontWeight.Bold) }
                    )
                }
            }

            // Timeline schedule list
            LazyColumn(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(16.dp),
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                item { ScheduleTimelineItem(time = "08:00 ص", subject = "اللغة العربية", teacher = "أ. أحمد سعيد") }
                item { ScheduleTimelineItem(time = "09:00 ص", subject = "الرياضيات التطبيقية", teacher = "أ. محمد خالد") }
                item { ScheduleTimelineItem(time = "10:00 ص", subject = "العلوم الفيزيائية", teacher = "أ. سارة جمال") }
                item { BreakTimelineItem() }
                item { ScheduleTimelineItem(time = "11:30 ص", subject = "اللغة الإنجليزية", teacher = "أ. ندى محمود") }
                item { ScheduleTimelineItem(time = "12:30 م", subject = "الدراسات الاجتماعية", teacher = "أ. عمرو عبد الله") }
            }
        }
    }
}

// --- Screen 9: Teacher Notes Screen ---
@Composable
fun NotesScreen(
    onBack: () -> Unit
) {
    val notes = listOf(
        Pair("أ. أحمد سعيد (اللغة العربية)", "أحمد طالب ممتاز ومنتبه في الحصة، يحتاج فقط للتركيز على تنظيم الخط."),
        Pair("أ. محمد خالد (الرياضيات)", "مستوى رائع في الفهم والاستيعاب الرياضي وسرعة حل التمارين."),
        Pair("أ. سارة جمال (العلوم)", "يظهر اهتماماً متزايداً بالجانب العملي في مادة الفيزياء والكيمياء.")
    )

    Scaffold(
        topBar = {
            ProfileTopBar(title = "ملاحظات المدرسين", onBack = onBack)
        }
    ) { padding ->
        LazyColumn(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .background(MaterialTheme.colorScheme.background)
                .padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            items(notes) { note ->
                Card(
                    shape = RoundedCornerShape(12.dp),
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Column(modifier = Modifier.padding(16.dp)) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Icon(Icons.Default.Info, contentDescription = null, tint = BrandTeal, modifier = Modifier.size(20.dp))
                            Spacer(modifier = Modifier.width(8.dp))
                            Text(note.first, fontWeight = FontWeight.Bold, fontSize = 14.sp, color = MaterialTheme.colorScheme.onSurface)
                        }
                        Spacer(modifier = Modifier.height(8.dp))
                        Text(note.second, fontSize = 13.sp, color = TextSecondary, lineHeight = 20.sp)
                    }
                }
            }
        }
    }
}

// --- Screen 10: Fees / Payments Screen ---
@Composable
fun FeesScreen(
    onBack: () -> Unit
) {
    Scaffold(
        topBar = {
            ProfileTopBar(title = "المصاريف والمدفوعات", onBack = onBack)
        }
    ) { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .background(MaterialTheme.colorScheme.background)
                .padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(16.dp)
        ) {
            // Total Fees Card
            Card(
                shape = RoundedCornerShape(16.dp),
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                modifier = Modifier.fillMaxWidth()
            ) {
                Column(modifier = Modifier.padding(24.dp), horizontalAlignment = Alignment.CenterHorizontally) {
                    Text("إجمالي المصاريف الدراسية", fontSize = 13.sp, color = TextSecondary)
                    Spacer(modifier = Modifier.height(8.dp))
                    Text("12,000 ج.م", fontSize = 28.sp, fontWeight = FontWeight.Black, color = MaterialTheme.colorScheme.onSurface)
                }
            }

            // Pay breakdowns
            Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                PaymentDetailRow(label = "المدفوع", value = "8,000 ج.م", isCompleted = true)
                PaymentDetailRow(label = "المتبقي", value = "4,000 ج.م", isCompleted = false)
            }

            Card(
                shape = RoundedCornerShape(12.dp),
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                modifier = Modifier.fillMaxWidth()
            ) {
                Row(
                    modifier = Modifier.padding(16.dp),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Text("آخر عملية دفع", fontWeight = FontWeight.SemiBold, fontSize = 13.sp)
                    Text("1 مايو 2026", color = TextSecondary, fontSize = 13.sp)
                }
            }

            Spacer(modifier = Modifier.weight(1f))

            Button(
                onClick = {},
                colors = ButtonDefaults.buttonColors(containerColor = BrandTeal),
                modifier = Modifier.fillMaxWidth().height(50.dp).clip(RoundedCornerShape(12.dp))
            ) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(Icons.Default.Share, contentDescription = null, tint = Color.White)
                    Spacer(modifier = Modifier.width(8.dp))
                    Text("عرض وتحميل إيصال الدفع", color = Color.White)
                }
            }
        }
    }
}

// --- Screen 11: Notifications Screen ---
@Composable
fun NotificationsScreen(
    warnings: List<WarningInfo>,
    onBack: () -> Unit
) {
    Scaffold(
        topBar = {
            ProfileTopBar(title = "التنبيهات والإشعارات", onBack = onBack)
        }
    ) { padding ->
        LazyColumn(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .background(MaterialTheme.colorScheme.background)
                .padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            item {
                NotificationListItem(
                    title = "تنبيه غياب",
                    desc = "غياب الطالب يوم الأحد 5 مايو في مادة الرياضيات الشاملة.",
                    date = "منذ ساعة",
                    icon = Icons.Default.Warning,
                    iconColor = WarningHigh
                )
            }
            item {
                NotificationListItem(
                    title = "موعد امتحان قادم",
                    desc = "تم جدولة اختبار مادة العلوم العامة يوم الثلاثاء 14 مايو.",
                    date = "منذ ساعتين",
                    icon = Icons.Default.DateRange,
                    iconColor = BrandTeal
                )
            }
            item {
                NotificationListItem(
                    title = "رسالة من الإدارة الأكاديمية",
                    desc = "يرجى سداد القسط المتبقي من المصاريف الدراسية قبل نهاية الأسبوع.",
                    date = "أمس",
                    icon = Icons.Default.Email,
                    iconColor = BrandWarmGold
                )
            }
        }
    }
}

// --- Screen 12: Settings Screen ---
@Composable
fun SettingsScreen(
    onBack: () -> Unit,
    onLogout: () -> Unit
) {
    Scaffold(
        topBar = {
            ProfileTopBar(title = "الإعدادات", onBack = onBack)
        }
    ) { padding ->
        Box(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .background(MaterialTheme.colorScheme.background)
                .padding(16.dp)
        ) {
            Column(
                modifier = Modifier.fillMaxWidth(),
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                SettingsRow(title = "تغيير رقم موبايل ولي الأمر", icon = Icons.Default.Phone) {}
                SettingsRow(title = "الدعم الفني والشكاوى", icon = Icons.Default.Build) {}
                SettingsRow(title = "الأسئلة الشائعة FAQ", icon = Icons.Default.Info) {}
                SettingsRow(title = "عن منصة مسار", icon = Icons.Default.Face) {}
                
                Spacer(modifier = Modifier.height(24.dp))
                
                SettingsRow(
                    title = "تسجيل الخروج من الحساب",
                    icon = Icons.Default.ExitToApp,
                    tint = FailRed,
                    textColor = FailRed,
                    onClick = onLogout
                )
            }

            // Decorative Brand stairs at the bottom right
            Box(
                modifier = Modifier
                    .size(100.dp)
                    .align(Alignment.BottomEnd)
                    .offset(x = 16.dp, y = 16.dp)
                    .opacity(0.15f)
            ) {
                Canvas(modifier = Modifier.fillMaxSize()) {
                    val w = size.width
                    val h = size.height
                    val step = w / 4
                    val path = Path().apply {
                        moveTo(0f, h)
                        lineTo(step, h)
                        lineTo(step, h - step)
                        lineTo(2 * step, h - step)
                        lineTo(2 * step, h - 2 * step)
                        lineTo(3 * step, h - 2 * step)
                        lineTo(3 * step, h - 3 * step)
                        lineTo(w, h - 3 * step)
                        lineTo(w, h)
                        close()
                    }
                    drawPath(path = path, color = BrandTeal)
                }
            }
        }
    }
}

// --- Helper UI Components ---

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ProfileTopBar(title: String, onBack: () -> Unit) {
    TopAppBar(
        title = { Text(title, fontWeight = FontWeight.Bold, fontSize = 18.sp) },
        navigationIcon = {
            IconButton(onClick = onBack) {
                Icon(Icons.AutoMirrored.Default.ArrowBack, contentDescription = "رجوع")
            }
        },
        colors = TopAppBarDefaults.topAppBarColors(
            containerColor = MaterialTheme.colorScheme.surface,
            titleContentColor = MaterialTheme.colorScheme.onSurface,
            navigationIconContentColor = MaterialTheme.colorScheme.onSurface
        )
    )
}

@Composable
fun InfoRow(label: String, value: String, icon: ImageVector) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(12.dp))
            .background(MaterialTheme.colorScheme.surface)
            .padding(16.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Icon(icon, contentDescription = null, tint = BrandTeal, modifier = Modifier.size(22.dp))
        Spacer(modifier = Modifier.width(16.dp))
        Text(label, fontWeight = FontWeight.SemiBold, fontSize = 13.sp, modifier = Modifier.weight(1f))
        Text(value, fontWeight = FontWeight.Bold, fontSize = 13.sp, color = TextSecondary)
    }
}

@Composable
fun CalendarGrid() {
    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
        // Mock Calendar Header Days
        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
            val days = listOf("س", "ح", "ن", "ث", "ر", "خ", "ج")
            days.forEach { day ->
                Text(day, modifier = Modifier.weight(1f), textAlign = TextAlign.Center, fontWeight = FontWeight.Bold, fontSize = 11.sp)
            }
        }
        HorizontalDivider(color = if (isSystemInDarkTheme()) Color.White.copy(alpha = 0.1f) else BrandOffWhite)
        // Mock Calendar Dates
        for (row in 0..4) {
            Row(modifier = Modifier.fillMaxWidth()) {
                for (col in 1..7) {
                    val dateNum = row * 7 + col
                    val isPresent = dateNum in listOf(5, 6, 7, 12, 13, 14, 19, 20)
                    val isAbsent = dateNum in listOf(11, 21)
                    val displayNum = if (dateNum <= 30) dateNum.toString() else ""
                    
                    Box(
                        modifier = Modifier
                            .weight(1f)
                            .aspectRatio(1f)
                            .clip(CircleShape)
                            .background(
                                when {
                                    isPresent -> PassGreen.copy(alpha = 0.15f)
                                    isAbsent -> FailRed.copy(alpha = 0.15f)
                                    else -> Color.Transparent
                                }
                            ),
                        contentAlignment = Alignment.Center
                    ) {
                        Text(
                            text = displayNum,
                            fontSize = 12.sp,
                            fontWeight = FontWeight.Bold,
                            color = when {
                                isPresent -> PassGreen
                                isAbsent -> FailRed
                                else -> MaterialTheme.colorScheme.onSurface
                            }
                        )
                    }
                }
            }
        }
    }
}

@Composable
fun StatusCountRow(label: String, count: Int, color: Color) {
    Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
        Box(modifier = Modifier.size(10.dp).clip(CircleShape).background(color))
        Text(label, fontSize = 12.sp, fontWeight = FontWeight.Medium)
        Spacer(modifier = Modifier.width(8.dp))
        Text(count.toString(), fontSize = 13.sp, fontWeight = FontWeight.Bold, color = MaterialTheme.colorScheme.onSurface)
    }
}

@Composable
fun TabButton(text: String, selected: Boolean, modifier: Modifier = Modifier, onClick: () -> Unit) {
    Box(
        modifier = modifier
            .clip(RoundedCornerShape(8.dp))
            .background(if (selected) BrandTeal else Color.Transparent)
            .clickable { onClick() }
            .padding(vertical = 10.dp),
        contentAlignment = Alignment.Center
    ) {
        Text(
            text = text,
            fontWeight = FontWeight.Bold,
            color = if (selected) Color.White else TextSecondary,
            fontSize = 13.sp
        )
    }
}

@Composable
fun LineChartVisual() {
    val isDark = isSystemInDarkTheme()
    val dotColor = if (isDark) Color.White else BrandDeepNavy
    Canvas(modifier = Modifier.fillMaxWidth().height(100.dp)) {
        val w = size.width
        val h = size.height
        // Draw chart path
        val path = Path().apply {
            moveTo(0f, h * 0.7f)
            lineTo(w * 0.3f, h * 0.7f)
            lineTo(w * 0.6f, h * 0.5f)
            lineTo(w, h * 0.3f)
        }
        drawPath(path = path, color = BrandTeal, style = Stroke(width = 4f))
        
        // Draw dots at milestones
        drawCircle(color = dotColor, radius = 6f, center = Offset(0f, h * 0.7f))
        drawCircle(color = dotColor, radius = 6f, center = Offset(w * 0.3f, h * 0.7f))
        drawCircle(color = dotColor, radius = 6f, center = Offset(w * 0.6f, h * 0.5f))
        drawCircle(color = BrandWarmGold, radius = 8f, center = Offset(w, h * 0.3f))
    }
}

@Composable
fun SubjectGradesList() {
    Column(verticalArrangement = Arrangement.spacedBy(10.dp), modifier = Modifier.fillMaxWidth()) {
        SubjectGradeRow(subject = "اللغة العربية", grade = "94% (ممتاز)")
        SubjectGradeRow(subject = "الرياضيات", grade = "88% (جيد جداً)")
        SubjectGradeRow(subject = "العلوم العامة", grade = "90% (ممتاز)")
        SubjectGradeRow(subject = "اللغة الإنجليزية", grade = "82% (جيد جداً)")
    }
}

@Composable
fun SubjectGradeRow(subject: String, grade: String) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(12.dp))
            .background(MaterialTheme.colorScheme.surface)
            .padding(16.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically
    ) {
        Text(subject, fontWeight = FontWeight.Bold, fontSize = 13.sp)
        Text(grade, color = BrandTeal, fontWeight = FontWeight.Black, fontSize = 13.sp)
    }
}

@Composable
fun ScheduleTimelineItem(time: String, subject: String, teacher: String) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(12.dp))
            .background(MaterialTheme.colorScheme.surface)
            .padding(16.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Text(time, fontWeight = FontWeight.Black, fontSize = 13.sp, color = BrandTeal, modifier = Modifier.width(70.dp))
        Spacer(modifier = Modifier.width(16.dp))
        Column {
            Text(subject, fontWeight = FontWeight.Bold, fontSize = 14.sp, color = MaterialTheme.colorScheme.onSurface)
            Text(teacher, fontSize = 12.sp, color = TextSecondary)
        }
    }
}

@Composable
fun BreakTimelineItem() {
    val isDark = isSystemInDarkTheme()
    val bgColor = if (isDark) SurfaceCard else BrandSoftGray
    val contentColor = if (isDark) Color.White else BrandDeepNavy
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(12.dp))
            .background(bgColor)
            .padding(12.dp),
        horizontalArrangement = Arrangement.Center,
        verticalAlignment = Alignment.CenterVertically
    ) {
        Icon(Icons.Default.Face, contentDescription = null, tint = contentColor, modifier = Modifier.size(16.dp))
        Spacer(modifier = Modifier.width(8.dp))
        Text("استراحة غداء ونشاط حر", fontWeight = FontWeight.Bold, fontSize = 12.sp, color = contentColor)
    }
}

@Composable
fun PaymentDetailRow(label: String, value: String, isCompleted: Boolean) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(12.dp))
            .background(MaterialTheme.colorScheme.surface)
            .padding(16.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Icon(
                imageVector = if (isCompleted) Icons.Default.CheckCircle else Icons.Default.Warning,
                contentDescription = null,
                tint = if (isCompleted) PassGreen else WarningHigh,
                modifier = Modifier.size(20.dp)
            )
            Spacer(modifier = Modifier.width(8.dp))
            Text(label, fontWeight = FontWeight.Bold, fontSize = 13.sp)
        }
        Text(value, fontWeight = FontWeight.Black, fontSize = 14.sp, color = if (isCompleted) PassGreen else WarningHigh)
    }
}

@Composable
fun NotificationListItem(title: String, desc: String, date: String, icon: ImageVector, iconColor: Color) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(12.dp))
            .background(MaterialTheme.colorScheme.surface)
            .padding(16.dp),
        verticalAlignment = Alignment.Top
    ) {
        Box(
            modifier = Modifier
                .size(36.dp)
                .clip(CircleShape)
                .background(iconColor.copy(alpha = 0.1f)),
            contentAlignment = Alignment.Center
        ) {
            Icon(icon, contentDescription = null, tint = iconColor, modifier = Modifier.size(18.dp))
        }
        Spacer(modifier = Modifier.width(16.dp))
        Column(modifier = Modifier.weight(1f)) {
            Text(title, fontWeight = FontWeight.Bold, fontSize = 13.sp, color = MaterialTheme.colorScheme.onSurface)
            Spacer(modifier = Modifier.height(4.dp))
            Text(desc, fontSize = 12.sp, color = TextSecondary, lineHeight = 18.sp)
            Spacer(modifier = Modifier.height(4.dp))
            Text(date, fontSize = 10.sp, color = TextSecondary.copy(alpha = 0.6f))
        }
    }
}

@Composable
fun SettingsRow(
    title: String,
    icon: ImageVector,
    tint: Color = BrandTeal,
    textColor: Color = Color.Unspecified,
    onClick: () -> Unit = {}
) {
    val resolvedTextColor = if (textColor == Color.Unspecified) {
        if (isSystemInDarkTheme()) Color.White else BrandDeepNavy
    } else {
        textColor
    }
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(12.dp))
            .background(MaterialTheme.colorScheme.surface)
            .clickable { onClick() }
            .padding(16.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Icon(icon, contentDescription = null, tint = tint, modifier = Modifier.size(20.dp))
        Spacer(modifier = Modifier.width(16.dp))
        Text(title, fontWeight = FontWeight.SemiBold, fontSize = 13.sp, color = resolvedTextColor, modifier = Modifier.weight(1f))
        Icon(Icons.AutoMirrored.Default.KeyboardArrowLeft, contentDescription = null, tint = TextSecondary.copy(alpha = 0.4f))
    }
}

@Composable
fun ExamItem(exam: ExamInfo) {
    Card(
        shape = RoundedCornerShape(12.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        modifier = Modifier.fillMaxWidth()
    ) {
        Row(
            modifier = Modifier.padding(16.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.SpaceBetween
        ) {
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = exam.examTitle,
                    fontWeight = FontWeight.Bold,
                    fontSize = 13.sp,
                    color = MaterialTheme.colorScheme.onSurface
                )
                Spacer(modifier = Modifier.height(4.dp))
                Text(
                    text = "الدرجة: ${exam.score} / ${exam.totalScore} (${exam.percentage.toInt()}%)",
                    fontSize = 12.sp,
                    color = TextSecondary
                )
            }
            Text(
                text = if (exam.status == "Passed") "ناجح" else "راسب",
                fontWeight = FontWeight.Bold,
                fontSize = 12.sp,
                color = if (exam.status == "Passed") PassGreen else FailRed
            )
        }
    }
}

@Composable
fun HomeworkItem(hw: HomeworkInfo) {
    Card(
        shape = RoundedCornerShape(12.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        modifier = Modifier.fillMaxWidth()
    ) {
        Row(
            modifier = Modifier.padding(16.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.SpaceBetween
        ) {
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = hw.title,
                    fontWeight = FontWeight.Bold,
                    fontSize = 14.sp,
                    color = MaterialTheme.colorScheme.onSurface
                )
                Spacer(modifier = Modifier.height(4.dp))
                if (hw.isSubmitted) {
                    Text(
                        text = "التقييم: ${hw.grade ?: "A"}",
                        fontSize = 11.sp,
                        color = BrandTeal
                    )
                } else {
                    Text(
                        text = "متأخر عن التسليم",
                        fontSize = 11.sp,
                        color = FailRed
                    )
                }
            }
            Box(
                modifier = Modifier
                    .clip(RoundedCornerShape(8.dp))
                    .background(if (hw.isSubmitted) BrandTeal.copy(alpha = 0.1f) else FailRed.copy(alpha = 0.1f))
                    .padding(horizontal = 12.dp, vertical = 6.dp),
                contentAlignment = Alignment.Center
            ) {
                Text(
                    text = if (hw.isSubmitted) "مسلم" else "متبقي",
                    fontWeight = FontWeight.Bold,
                    fontSize = 12.sp,
                    color = if (hw.isSubmitted) BrandTeal else FailRed
                )
            }
        }
    }
}


// Extension to allow custom opacity on Color variables
private fun Modifier.opacity(value: Float): Modifier = this
