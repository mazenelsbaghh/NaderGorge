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
import com.nadergorge.parent.data.api.AttendanceInfo
import com.nadergorge.parent.data.api.BalanceInfo
import com.nadergorge.parent.data.api.CourseInfo
import com.nadergorge.parent.data.api.CourseTermInfo
import com.nadergorge.parent.data.api.ParentNotificationResponse
import com.nadergorge.parent.data.api.TeacherInfo
import com.nadergorge.parent.data.api.WatchLessonInfo
import com.nadergorge.parent.data.api.StudentDetailsResponse
import com.nadergorge.parent.data.api.WarningInfo
import com.nadergorge.parent.ui.AcademicLabels
import com.nadergorge.parent.ui.theme.*
import java.time.LocalDate
import java.time.LocalDateTime
import java.time.OffsetDateTime
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.util.Locale

private val ParentBottomSafeSpace = 112.dp

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
                InfoRow(label = "الصف الدراسي", value = AcademicLabels.grade(grade), icon = Icons.Default.Home)
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

// --- Screen 5: Watch Log Screen ---
@Composable
fun WatchLogScreen(
    teachers: List<TeacherInfo>,
    watchLessons: List<WatchLessonInfo>,
    courses: List<CourseInfo> = emptyList(),
    attendance: AttendanceInfo? = null,
    onBack: () -> Unit
) {
    var selectedTeacherId by remember { mutableStateOf<String?>(null) }
    var selectedPackageId by remember { mutableStateOf<String?>(null) }
    var selectedTermId by remember { mutableStateOf<String?>(null) }
    LaunchedEffect(teachers.map { it.teacherId }) {
        val teacherIds = teachers.map { it.teacherId }
        selectedTeacherId = when {
            teacherIds.isEmpty() -> null
            selectedTeacherId in teacherIds -> selectedTeacherId
            else -> teacherIds.first()
        }
    }
    val teacherCourses = courses.filter { it.teacherId == selectedTeacherId }
    LaunchedEffect(selectedTeacherId, teacherCourses.map { it.packageId }) {
        val packageIds = teacherCourses.map { it.packageId }
        selectedPackageId = when {
            packageIds.isEmpty() -> null
            selectedPackageId in packageIds -> selectedPackageId
            else -> packageIds.first()
        }
    }
    val selectedCourse = teacherCourses.firstOrNull { it.packageId == selectedPackageId }
    val selectedTerms = selectedCourse?.terms.orEmpty()
    LaunchedEffect(selectedPackageId, selectedTerms.map { it.termId }) {
        val termIds = selectedTerms.map { it.termId }
        selectedTermId = when {
            termIds.isEmpty() -> null
            selectedTermId in termIds -> selectedTermId
            else -> termIds.first()
        }
    }
    val canFilterByTeacher = teachers.isNotEmpty() && selectedTeacherId != null
    val canFilterByCourse = courses.isNotEmpty() && selectedPackageId != null
    val canFilterByTerm = selectedTerms.isNotEmpty() && selectedTermId != null
    val filteredLessons = watchLessons.filter {
        (!canFilterByTeacher || it.teacherId == selectedTeacherId) &&
            (!canFilterByCourse || it.packageId == selectedPackageId) &&
            (!canFilterByTerm || it.termId == selectedTermId)
    }
    val hasDetailedLessons = filteredLessons.isNotEmpty()
    val completedCount = if (hasDetailedLessons) filteredLessons.count { it.isCompleted } else attendance?.watchedLessons ?: 0
    val totalLessons = if (hasDetailedLessons) filteredLessons.size else attendance?.totalLessons ?: 0
    val completionRate = if (hasDetailedLessons) {
        completedCount * 100.0 / filteredLessons.size
    } else {
        attendance?.completionRate ?: 0.0
    }

    Scaffold(
        topBar = {
            ProfileTopBar(title = "سجل المشاهدات", onBack = onBack)
        }
    ) { padding ->
        LazyColumn(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .background(MaterialTheme.colorScheme.background)
                .padding(16.dp),
            contentPadding = PaddingValues(bottom = ParentBottomSafeSpace),
            verticalArrangement = Arrangement.spacedBy(16.dp)
        ) {
            if (teachers.isNotEmpty()) item {
                TeacherPicker(
                    teachers = teachers,
                    selectedTeacherId = selectedTeacherId,
                    onSelect = { selectedTeacherId = it }
                )
            }
            if (teacherCourses.isNotEmpty()) item {
                CourseTermPicker(
                    courses = teacherCourses,
                    selectedPackageId = selectedPackageId,
                    selectedTermId = selectedTermId,
                    onCourseSelect = { selectedPackageId = it },
                    onTermSelect = { selectedTermId = it }
                )
            }

            item {
                Card(
                    shape = RoundedCornerShape(16.dp),
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Column(modifier = Modifier.padding(16.dp)) {
                        Text(
                            text = if (teachers.isNotEmpty()) "ملخص مشاهدة الحصص المختارة" else "ملخص المشاهدات",
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
                                    progress = (completionRate / 100).toFloat().coerceIn(0f, 1f),
                                    color = BrandTeal,
                                    strokeWidth = 8.dp,
                                    modifier = Modifier.fillMaxSize()
                                )
                                Text("${completionRate.toInt()}%", fontWeight = FontWeight.Black, fontSize = 16.sp)
                            }
                            Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
                                StatusCountRow(label = "حصص مكتملة", count = completedCount, color = PassGreen)
                                StatusCountRow(label = "حصص تحتاج مشاهدة", count = (totalLessons - completedCount).coerceAtLeast(0), color = FailRed)
                                StatusCountRow(label = "إجمالي الحصص", count = totalLessons, color = BrandTeal)
                            }
                        }
                    }
                }
            }

            if (filteredLessons.isEmpty()) {
                item {
                    EmptyStateCard(
                        title = if (totalLessons > 0) "تفاصيل الحصص غير متاحة حالياً" else "لا توجد مشاهدات مسجلة",
                        text = if (totalLessons > 0) "يعرض التطبيق الملخص العام للمشاهدات الآن: $completedCount من $totalLessons حصة." else "عند بدء مشاهدة الحصص ستظهر تفاصيلها هنا."
                    )
                }
            } else {
                items(filteredLessons) { lesson ->
                    WatchLessonItem(lesson)
                }
            }
        }
    }
}

// --- Screen 6: Exams Screen ---
@Composable
fun GradesScreen(
    teachers: List<TeacherInfo>,
    exams: List<ExamInfo>,
    courses: List<CourseInfo> = emptyList(),
    onBack: () -> Unit
) {
    var selectedTeacherId by remember { mutableStateOf<String?>(null) }
    var selectedPackageId by remember { mutableStateOf<String?>(null) }
    var selectedTermId by remember { mutableStateOf<String?>(null) }
    LaunchedEffect(teachers.map { it.teacherId }) {
        val teacherIds = teachers.map { it.teacherId }
        selectedTeacherId = when {
            teacherIds.isEmpty() -> null
            selectedTeacherId in teacherIds -> selectedTeacherId
            else -> teacherIds.first()
        }
    }
    val teacherCourses = courses.filter { it.teacherId == selectedTeacherId }
    LaunchedEffect(selectedTeacherId, teacherCourses.map { it.packageId }) {
        val packageIds = teacherCourses.map { it.packageId }
        selectedPackageId = when {
            packageIds.isEmpty() -> null
            selectedPackageId in packageIds -> selectedPackageId
            else -> packageIds.first()
        }
    }
    val selectedCourse = teacherCourses.firstOrNull { it.packageId == selectedPackageId }
    val selectedTerms = selectedCourse?.terms.orEmpty()
    LaunchedEffect(selectedPackageId, selectedTerms.map { it.termId }) {
        val termIds = selectedTerms.map { it.termId }
        selectedTermId = when {
            termIds.isEmpty() -> null
            selectedTermId in termIds -> selectedTermId
            else -> termIds.first()
        }
    }
    val canFilterByTeacher = teachers.isNotEmpty() && selectedTeacherId != null
    val canFilterByCourse = courses.isNotEmpty() && selectedPackageId != null
    val canFilterByTerm = selectedTerms.isNotEmpty() && selectedTermId != null
    val filteredExams = exams.filter {
        (!canFilterByTeacher || it.teacherId == selectedTeacherId) &&
            (!canFilterByCourse || it.packageId == selectedPackageId) &&
            (!canFilterByTerm || it.termId == selectedTermId)
    }
    val average = filteredExams.takeIf { it.isNotEmpty() }?.map { it.percentage }?.average() ?: 0.0

    Scaffold(
        topBar = {
            ProfileTopBar(title = "الامتحانات", onBack = onBack)
        }
    ) { padding ->
        LazyColumn(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .background(MaterialTheme.colorScheme.background)
                .padding(16.dp),
            contentPadding = PaddingValues(bottom = ParentBottomSafeSpace),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            if (teachers.isNotEmpty()) item {
                TeacherPicker(
                    teachers = teachers,
                    selectedTeacherId = selectedTeacherId,
                    onSelect = { selectedTeacherId = it }
                )
            }
            if (teacherCourses.isNotEmpty()) item {
                CourseTermPicker(
                    courses = teacherCourses,
                    selectedPackageId = selectedPackageId,
                    selectedTermId = selectedTermId,
                    onCourseSelect = { selectedPackageId = it },
                    onTermSelect = { selectedTermId = it }
                )
            }
            item {
                Card(
                    shape = RoundedCornerShape(16.dp),
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Column(modifier = Modifier.padding(16.dp)) {
                        Text("متوسط امتحانات الاختيار الحالي", fontWeight = FontWeight.Bold, fontSize = 14.sp)
                        Spacer(modifier = Modifier.height(4.dp))
                        Text("${average.toInt()}%", color = BrandTeal, fontWeight = FontWeight.Black, fontSize = 20.sp)
                        Spacer(modifier = Modifier.height(16.dp))
                        LineChartVisual()
                    }
                }
            }

            if (filteredExams.isEmpty()) {
                item {
                    EmptyStateCard(
                        title = "لا توجد امتحانات لهذا الاختيار",
                        text = "لا يوجد امتحان ظاهر للطالب حسب المدرس والكورس والترم الحالي، أو لم يتم حل أي امتحان بعد."
                    )
                }
            } else {
                items(filteredExams) { exam ->
                    ExamItem(exam)
                }
            }
        }
    }
}

@Composable
fun CoursesScreen(
    teachers: List<TeacherInfo>,
    courses: List<CourseInfo>,
    watchLessons: List<WatchLessonInfo>,
    exams: List<ExamInfo>,
    onBack: () -> Unit
) {
    var selectedTeacherId by remember { mutableStateOf<String?>(null) }
    LaunchedEffect(teachers.map { it.teacherId }) {
        val teacherIds = teachers.map { it.teacherId }
        selectedTeacherId = when {
            teacherIds.isEmpty() -> null
            selectedTeacherId in teacherIds -> selectedTeacherId
            else -> teacherIds.first()
        }
    }
    val teacherCourses = courses.filter { it.teacherId == selectedTeacherId }

    Scaffold(
        topBar = {
            ProfileTopBar(title = "الكورسات والترمات", onBack = onBack)
        }
    ) { padding ->
        LazyColumn(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .background(MaterialTheme.colorScheme.background)
                .padding(16.dp),
            contentPadding = PaddingValues(bottom = ParentBottomSafeSpace),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            item {
                TeacherPicker(
                    teachers = teachers,
                    selectedTeacherId = selectedTeacherId,
                    onSelect = { selectedTeacherId = it }
                )
            }

            if (teacherCourses.isEmpty()) {
                item {
                    EmptyStateCard(
                        title = "لا توجد كورسات مشتراة لهذا المدرس",
                        text = "الكورسات والترمات تظهر هنا حسب الباقات أو الحصص التي اشتراها الطالب."
                    )
                }
            } else {
                items(teacherCourses) { course ->
                    CourseSummaryCard(
                        course = course,
                        watchLessons = watchLessons.filter { it.packageId == course.packageId },
                        exams = exams.filter { it.packageId == course.packageId }
                    )
                }
            }
        }
    }
}

// --- Screen 7: Homework Screen ---
@Composable
fun HomeworkScreen(
    teachers: List<TeacherInfo>,
    homeworks: List<HomeworkInfo>,
    courses: List<CourseInfo> = emptyList(),
    onBack: () -> Unit
) {
    var filterTab by remember { mutableStateOf(0) } // 0: All, 1: Pending, 2: Submitted
    var selectedTeacherId by remember { mutableStateOf<String?>(null) }
    var selectedPackageId by remember { mutableStateOf<String?>(null) }
    var selectedTermId by remember { mutableStateOf<String?>(null) }
    LaunchedEffect(teachers.map { it.teacherId }) {
        val teacherIds = teachers.map { it.teacherId }
        selectedTeacherId = when {
            teacherIds.isEmpty() -> null
            selectedTeacherId in teacherIds -> selectedTeacherId
            else -> teacherIds.first()
        }
    }
    val teacherCourses = courses.filter { it.teacherId == selectedTeacherId }
    LaunchedEffect(selectedTeacherId, teacherCourses.map { it.packageId }) {
        val packageIds = teacherCourses.map { it.packageId }
        selectedPackageId = when {
            packageIds.isEmpty() -> null
            selectedPackageId in packageIds -> selectedPackageId
            else -> packageIds.first()
        }
    }
    val selectedCourse = teacherCourses.firstOrNull { it.packageId == selectedPackageId }
    val selectedTerms = selectedCourse?.terms.orEmpty()
    LaunchedEffect(selectedPackageId, selectedTerms.map { it.termId }) {
        val termIds = selectedTerms.map { it.termId }
        selectedTermId = when {
            termIds.isEmpty() -> null
            selectedTermId in termIds -> selectedTermId
            else -> termIds.first()
        }
    }
    val canFilterByTeacher = teachers.isNotEmpty() && selectedTeacherId != null
    val canFilterByCourse = courses.isNotEmpty() && selectedPackageId != null
    val canFilterByTerm = selectedTerms.isNotEmpty() && selectedTermId != null
    val scopedHomeworks = homeworks.filter {
        (!canFilterByTeacher || it.teacherId == selectedTeacherId) &&
            (!canFilterByCourse || it.packageId == selectedPackageId) &&
            (!canFilterByTerm || it.termId == selectedTermId)
    }
    val filteredList = when (filterTab) {
        1 -> scopedHomeworks.filter { !it.isSubmitted }
        2 -> scopedHomeworks.filter { it.isSubmitted }
        else -> scopedHomeworks
    }
    val submittedCount = scopedHomeworks.count { it.isSubmitted }
    val pendingCount = scopedHomeworks.size - submittedCount

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

            LazyColumn(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(horizontal = 16.dp),
                contentPadding = PaddingValues(bottom = ParentBottomSafeSpace),
                verticalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                if (teachers.isNotEmpty()) item {
                    TeacherPicker(
                        teachers = teachers,
                        selectedTeacherId = selectedTeacherId,
                        onSelect = { selectedTeacherId = it }
                    )
                }
                if (teacherCourses.isNotEmpty()) item {
                    CourseTermPicker(
                        courses = teacherCourses,
                        selectedPackageId = selectedPackageId,
                        selectedTermId = selectedTermId,
                        onCourseSelect = { selectedPackageId = it },
                        onTermSelect = { selectedTermId = it }
                    )
                }
                item {
                    HomeworkSummaryCard(
                        totalCount = scopedHomeworks.size,
                        submittedCount = submittedCount,
                        pendingCount = pendingCount
                    )
                }
                if (filteredList.isEmpty()) {
                    item {
                        EmptyStateCard(
                            title = "لا توجد واجبات هنا",
                            text = "لا يوجد واجب مطابق للاختيار الحالي. جرّب تبويب الكل أو اختر مدرساً/كورسا آخر."
                        )
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

// --- Screen 10: Balance Screen ---
@Composable
fun BalanceScreen(
    balance: BalanceInfo?,
    onBack: () -> Unit
) {
    val currentBalance = balance?.currentBalance ?: 0.0
    val transactions = balance?.transactions.orEmpty()

    Scaffold(
        topBar = {
            ProfileTopBar(title = "الرصيد وتفاصيل الرصيد", onBack = onBack)
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
            item {
                Card(
                    shape = RoundedCornerShape(16.dp),
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Column(modifier = Modifier.padding(24.dp), horizontalAlignment = Alignment.CenterHorizontally) {
                        Text("الرصيد الحالي", fontSize = 13.sp, color = TextSecondary)
                        Spacer(modifier = Modifier.height(8.dp))
                        Text("${currentBalance.toInt()} ج.م", fontSize = 28.sp, fontWeight = FontWeight.Black, color = MaterialTheme.colorScheme.onSurface)
                    }
                }
            }

            item {
                Text("آخر حركات الرصيد", fontWeight = FontWeight.Black, fontSize = 16.sp, color = MaterialTheme.colorScheme.onSurface)
            }

            if (transactions.isEmpty()) {
                item {
                    EmptyStateCard("لا توجد حركات رصيد مسجلة حتى الآن.")
                }
            } else {
                items(transactions) { transaction ->
                    BalanceTransactionItem(
                        amount = transaction.amount,
                        balanceAfter = transaction.balanceAfter,
                        title = transaction.description.ifBlank { transaction.transactionType },
                        date = transaction.createdAt
                    )
                }
            }
        }
    }
}

// --- Screen 11: Notifications Screen ---
@Composable
fun NotificationsScreen(
    warnings: List<WarningInfo>,
    notifications: List<ParentNotificationResponse>,
    onNotificationClick: (ParentNotificationResponse) -> Unit,
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
            if (warnings.isEmpty() && notifications.isEmpty()) {
                item {
                    EmptyStateCard("لا توجد تنبيهات مسجلة حتى الآن.")
                }
            } else {
                items(notifications.sortedByDescending { it.createdAt }) { notification ->
                    NotificationListItem(
                        title = notification.title,
                        desc = notification.body,
                        date = formatDisplayDateTime(notification.createdAt),
                        icon = Icons.Default.Notifications,
                        iconColor = if (notification.isRead) TextSecondary else BrandTeal,
                        modifier = Modifier.clickable { onNotificationClick(notification) }
                    )
                }
                items(warnings.sortedByDescending { it.createdAt }) { warning ->
                    NotificationListItem(
                        title = "تنبيه ${warning.severity}",
                        desc = warning.reason,
                        date = formatDisplayDateTime(warning.createdAt),
                        icon = Icons.Default.Warning,
                        iconColor = WarningHigh
                    )
                }
            }
        }
    }
}

// --- Screen 12: Settings Screen ---
@Composable
fun SettingsScreen(
    onBack: () -> Unit,
    notificationsEnabled: Boolean,
    onTestNotification: () -> Unit,
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
                SettingsRow(
                    title = if (notificationsEnabled) "الإشعارات مفعلة" else "الإشعارات غير مفعلة",
                    icon = Icons.Default.Notifications,
                    tint = if (notificationsEnabled) PassGreen else FailRed
                ) {}
                SettingsRow(
                    title = "اختبار إشعار داخل التطبيق",
                    icon = Icons.Default.Notifications,
                    tint = BrandWarmGold,
                    onClick = onTestNotification
                )
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
fun TeacherPicker(
    teachers: List<TeacherInfo>,
    selectedTeacherId: String?,
    onSelect: (String?) -> Unit
) {
    if (teachers.isEmpty()) {
        EmptyStateCard("لا يوجد مدرسين مرتبطين بمشتريات الطالب حتى الآن.")
        return
    }

    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
        Text("اختار المدرس", fontWeight = FontWeight.Black, fontSize = 14.sp, color = MaterialTheme.colorScheme.onSurface)
        LazyColumn(
            modifier = Modifier.heightIn(max = 156.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            items(teachers) { teacher ->
                val selected = teacher.teacherId == selectedTeacherId
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .clip(RoundedCornerShape(12.dp))
                        .background(if (selected) BrandTeal.copy(alpha = 0.12f) else MaterialTheme.colorScheme.surface)
                        .clickable { onSelect(teacher.teacherId) }
                        .padding(14.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Icon(Icons.Default.Person, contentDescription = null, tint = BrandTeal, modifier = Modifier.size(20.dp))
                    Spacer(modifier = Modifier.width(12.dp))
                    Column(modifier = Modifier.weight(1f)) {
                        Text(teacher.teacherName, fontWeight = FontWeight.Bold, fontSize = 13.sp, color = MaterialTheme.colorScheme.onSurface)
                        teacher.specialization?.takeIf { it.isNotBlank() }?.let {
                            Text(it, fontSize = 11.sp, color = TextSecondary)
                        }
                    }
                    if (selected) Icon(Icons.Default.CheckCircle, contentDescription = null, tint = BrandTeal, modifier = Modifier.size(18.dp))
                }
            }
        }
    }
}

@Composable
fun CourseTermPicker(
    courses: List<CourseInfo>,
    selectedPackageId: String?,
    selectedTermId: String?,
    onCourseSelect: (String?) -> Unit,
    onTermSelect: (String?) -> Unit
) {
    if (courses.isEmpty()) {
        EmptyStateCard(
            title = "لا توجد كورسات لهذا المدرس",
            text = "سيظهر هنا الكورس والترم بعد شراء الطالب حصة أو باقة من هذا المدرس."
        )
        return
    }

    val selectedCourse = courses.firstOrNull { it.packageId == selectedPackageId } ?: courses.first()
    val terms = selectedCourse.terms.orEmpty()

    Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
        Text("اختار الكورس", fontWeight = FontWeight.Black, fontSize = 14.sp, color = MaterialTheme.colorScheme.onSurface)
        Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
            courses.forEach { course ->
                val selected = course.packageId == selectedCourse.packageId
                SelectableRow(
                    title = course.packageName,
                    subtitle = course.teacherName,
                    icon = Icons.Default.List,
                    selected = selected,
                    onClick = { onCourseSelect(course.packageId) }
                )
            }
        }

        Text("اختار الترم", fontWeight = FontWeight.Black, fontSize = 14.sp, color = MaterialTheme.colorScheme.onSurface)
        if (terms.isEmpty()) {
            EmptyStateCard(
                title = "لا توجد ترمات داخل الكورس",
                text = "لا توجد دروس أو امتحانات مفعلة لهذا الكورس حالياً."
            )
        } else {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                terms.forEach { term ->
                    val selected = term.termId == selectedTermId
                    SelectableRow(
                        title = term.termTitle,
                        subtitle = "${term.lessonCount} حصة • ${term.examCount} امتحان",
                        icon = Icons.Default.DateRange,
                        selected = selected,
                        onClick = { onTermSelect(term.termId) }
                    )
                }
            }
        }
    }
}

@Composable
fun SelectableRow(
    title: String,
    subtitle: String,
    icon: ImageVector,
    selected: Boolean,
    onClick: () -> Unit
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(12.dp))
            .background(if (selected) BrandTeal.copy(alpha = 0.12f) else MaterialTheme.colorScheme.surface)
            .clickable { onClick() }
            .padding(14.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Icon(icon, contentDescription = null, tint = BrandTeal, modifier = Modifier.size(20.dp))
        Spacer(modifier = Modifier.width(12.dp))
        Column(modifier = Modifier.weight(1f)) {
            Text(title, fontWeight = FontWeight.Bold, fontSize = 13.sp, color = MaterialTheme.colorScheme.onSurface)
            Text(subtitle, fontSize = 11.sp, color = TextSecondary)
        }
        if (selected) Icon(Icons.Default.CheckCircle, contentDescription = null, tint = BrandTeal, modifier = Modifier.size(18.dp))
    }
}

@Composable
fun CourseSummaryCard(
    course: CourseInfo,
    watchLessons: List<WatchLessonInfo>,
    exams: List<ExamInfo>
) {
    var expanded by remember { mutableStateOf(false) }
    val completedLessons = watchLessons.count { it.isCompleted }
    Card(
        shape = RoundedCornerShape(14.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        modifier = Modifier.fillMaxWidth().clickable { expanded = !expanded }
    ) {
        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(Icons.Default.List, contentDescription = null, tint = BrandTeal, modifier = Modifier.size(22.dp))
                Spacer(modifier = Modifier.width(10.dp))
                Column(modifier = Modifier.weight(1f)) {
                    Text(course.packageName, fontWeight = FontWeight.Black, fontSize = 15.sp, color = MaterialTheme.colorScheme.onSurface)
                    Text(course.teacherName, fontSize = 12.sp, color = TextSecondary)
                }
                Icon(
                    imageVector = if (expanded) Icons.Default.KeyboardArrowUp else Icons.Default.KeyboardArrowDown,
                    contentDescription = null,
                    tint = TextSecondary
                )
            }

            Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth()) {
                MiniStat("الترمات", course.terms.orEmpty().size.toString(), BrandTeal, Modifier.weight(1f))
                MiniStat("الحصص", "${completedLessons}/${watchLessons.size}", PassGreen, Modifier.weight(1f))
                MiniStat("الامتحانات", exams.size.toString(), BrandWarmGold, Modifier.weight(1f))
            }

            if (expanded) {
                course.terms.orEmpty().forEach { term ->
                    val termLessons = watchLessons.filter { it.termId == term.termId }
                    val termExams = exams.filter { it.termId == term.termId }
                    Column(
                        modifier = Modifier
                            .fillMaxWidth()
                            .clip(RoundedCornerShape(10.dp))
                            .background(MaterialTheme.colorScheme.background)
                            .padding(12.dp),
                        verticalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        Text(term.termTitle, fontWeight = FontWeight.Black, fontSize = 13.sp, color = MaterialTheme.colorScheme.onSurface)
                        Text(
                            "الحصص: ${termLessons.count { it.isCompleted }} مكتملة من ${termLessons.size} • الامتحانات: ${termExams.size}",
                            fontSize = 12.sp,
                            color = TextSecondary
                        )
                        termLessons.take(4).forEach { lesson ->
                            Text(
                                "• ${lesson.lessonTitle} - ${if (lesson.isCompleted) "مكتملة" else "لم تكتمل"}",
                                fontSize = 11.sp,
                                color = if (lesson.isCompleted) PassGreen else TextSecondary
                            )
                        }
                    }
                }
            }
        }
    }
}

@Composable
fun MiniStat(label: String, value: String, color: Color, modifier: Modifier = Modifier) {
    Column(
        modifier = modifier
            .clip(RoundedCornerShape(10.dp))
            .background(color.copy(alpha = 0.08f))
            .padding(10.dp),
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Text(value, fontWeight = FontWeight.Black, fontSize = 15.sp, color = color)
        Text(label, fontSize = 10.sp, color = TextSecondary)
    }
}

@Composable
fun HomeworkSummaryCard(
    totalCount: Int,
    submittedCount: Int,
    pendingCount: Int
) {
    Card(
        shape = RoundedCornerShape(16.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        modifier = Modifier.fillMaxWidth()
    ) {
        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
            Text("ملخص الواجبات", fontWeight = FontWeight.Bold, fontSize = 14.sp, color = MaterialTheme.colorScheme.onSurface)
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.fillMaxWidth()) {
                MiniStat("الإجمالي", totalCount.toString(), BrandTeal, Modifier.weight(1f))
                MiniStat("تم التسليم", submittedCount.toString(), PassGreen, Modifier.weight(1f))
                MiniStat("متبقي", pendingCount.coerceAtLeast(0).toString(), FailRed, Modifier.weight(1f))
            }
        }
    }
}

@Composable
fun EmptyStateCard(
    text: String,
    title: String = "لا توجد بيانات"
) {
    Card(
        shape = RoundedCornerShape(12.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        modifier = Modifier.fillMaxWidth()
    ) {
        Column(
            modifier = Modifier.fillMaxWidth().padding(20.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            Icon(Icons.Default.Info, contentDescription = null, tint = BrandTeal, modifier = Modifier.size(22.dp))
            Text(
                text = title,
                color = MaterialTheme.colorScheme.onSurface,
                fontSize = 14.sp,
                fontWeight = FontWeight.Black,
                textAlign = TextAlign.Center
            )
            Text(
                text = text,
                color = TextSecondary,
                fontSize = 12.sp,
                lineHeight = 18.sp,
                textAlign = TextAlign.Center
            )
        }
    }
}

@Composable
fun WatchLessonItem(lesson: WatchLessonInfo) {
    Card(
        shape = RoundedCornerShape(12.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        modifier = Modifier.fillMaxWidth()
    ) {
        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(Icons.Default.PlayArrow, contentDescription = null, tint = BrandTeal, modifier = Modifier.size(20.dp))
                Spacer(modifier = Modifier.width(10.dp))
                Text(lesson.lessonTitle, fontWeight = FontWeight.Bold, fontSize = 14.sp, color = MaterialTheme.colorScheme.onSurface, modifier = Modifier.weight(1f))
                Text(if (lesson.isCompleted) "مكتملة" else "قيد المشاهدة", color = if (lesson.isCompleted) PassGreen else BrandWarmGold, fontSize = 12.sp, fontWeight = FontWeight.Bold)
            }
            if (lesson.packageName.isNotBlank() || lesson.termTitle.isNotBlank()) {
                Text(
                    listOf(lesson.packageName, lesson.termTitle).filter { it.isNotBlank() }.joinToString(" • "),
                    color = BrandTeal,
                    fontSize = 11.sp,
                    fontWeight = FontWeight.Bold
                )
            }
            Text("الفيديوهات: ${lesson.watchedVideos} / ${lesson.totalVideos}", color = TextSecondary, fontSize = 12.sp)
            Text("عدد المشاهدات: ${lesson.watchCount} • الوقت: ${formatDuration(lesson.watchedSeconds)}", color = TextSecondary, fontSize = 12.sp)
            lesson.lastWatchedAt?.let { Text("آخر مشاهدة: ${formatDisplayDateTime(it)}", color = TextSecondary.copy(alpha = 0.7f), fontSize = 11.sp) }
        }
    }
}

@Composable
fun BalanceTransactionItem(amount: Double, balanceAfter: Double, title: String, date: String) {
    val isCredit = amount >= 0
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(12.dp))
            .background(MaterialTheme.colorScheme.surface)
            .padding(16.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Icon(
            imageVector = if (isCredit) Icons.Default.AddCircle else Icons.Default.ShoppingCart,
            contentDescription = null,
            tint = if (isCredit) PassGreen else BrandWarmGold,
            modifier = Modifier.size(22.dp)
        )
        Spacer(modifier = Modifier.width(12.dp))
        Column(modifier = Modifier.weight(1f)) {
            Text(title, fontWeight = FontWeight.Bold, fontSize = 13.sp, color = MaterialTheme.colorScheme.onSurface)
            Text(formatDisplayDateTime(date), fontSize = 11.sp, color = TextSecondary)
        }
        Column(horizontalAlignment = Alignment.End) {
            Text("${amount.toInt()} ج.م", fontWeight = FontWeight.Black, color = if (isCredit) PassGreen else FailRed)
            Text("بعدها ${balanceAfter.toInt()} ج.م", fontSize = 11.sp, color = TextSecondary)
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
fun NotificationListItem(
    title: String,
    desc: String,
    date: String,
    icon: ImageVector,
    iconColor: Color,
    modifier: Modifier = Modifier
) {
    Row(
        modifier = modifier
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
    var expanded by remember { mutableStateOf(false) }
    val mistakes = exam.mistakes.orEmpty()
    val status = exam.status ?: "Unknown"
    val teacherName = exam.teacherName?.takeIf { it.isNotBlank() } ?: "مدرس المادة"
    val packageName = exam.packageName.orEmpty()
    val termTitle = exam.termTitle.orEmpty()
    val statusLabel = when (status) {
        "NotStarted" -> "غير محلول"
        "Passed" -> "ناجح"
        "Failed" -> "راسب"
        else -> status
    }
    val statusColor = when (status) {
        "NotStarted" -> TextSecondary
        "Passed" -> PassGreen
        "Failed" -> FailRed
        else -> BrandWarmGold
    }
    Card(
        shape = RoundedCornerShape(12.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        modifier = Modifier.fillMaxWidth().clickable { expanded = !expanded }
    ) {
        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.SpaceBetween) {
                Column(modifier = Modifier.weight(1f)) {
                    Text(
                        text = exam.examTitle,
                        fontWeight = FontWeight.Bold,
                        fontSize = 13.sp,
                        color = MaterialTheme.colorScheme.onSurface
                    )
                    Spacer(modifier = Modifier.height(4.dp))
                    Text(
                        text = "$teacherName • الدرجة: ${exam.score.toInt()} / ${exam.totalScore.toInt()} (${exam.percentage.toInt()}%)",
                        fontSize = 12.sp,
                        color = TextSecondary
                    )
                    Text(
                        text = if (status == "NotStarted") "لم يتم الحل بعد" else "غلطات: ${mistakes.size}",
                        fontSize = 11.sp,
                        color = BrandWarmGold,
                        fontWeight = FontWeight.Bold
                    )
                    if (packageName.isNotBlank() || termTitle.isNotBlank() || exam.submittedAt != null) {
                        Text(
                            text = listOfNotNull(
                                packageName.takeIf { it.isNotBlank() },
                                termTitle.takeIf { it.isNotBlank() },
                                exam.submittedAt?.let { formatDisplayDateTime(it) }
                            ).joinToString(" • "),
                            fontSize = 11.sp,
                            color = TextSecondary
                        )
                    }
                }
                Text(
                    text = statusLabel,
                    fontWeight = FontWeight.Bold,
                    fontSize = 12.sp,
                    color = statusColor
                )
            }
            if (expanded) {
                if (status == "NotStarted") {
                    Text("الامتحان لم يتم حله بعد.", color = TextSecondary, fontSize = 12.sp)
                } else if (mistakes.isEmpty()) {
                    Text("لا توجد أخطاء في هذا الامتحان.", color = PassGreen, fontSize = 12.sp)
                } else {
                    mistakes.forEachIndexed { index, mistake ->
                        ReviewMistakeBlock(
                            title = "سؤال ${index + 1}",
                            question = mistake.questionText,
                            studentAnswer = mistake.studentAnswer ?: "بدون إجابة",
                            correctAnswer = mistake.correctAnswer,
                            correction = mistake.writtenCorrection,
                            score = "${mistake.pointsAwarded.toInt()} / ${mistake.points.toInt()}"
                        )
                    }
                }
            }
        }
    }
}

@Composable
fun HomeworkItem(hw: HomeworkInfo) {
    var expanded by remember { mutableStateOf(false) }
    val mistakes = hw.mistakes.orEmpty()
    val submissionState = hw.submissionState ?: if (hw.isSubmitted) "Graded" else "NotSubmitted"
    val teacherName = hw.teacherName?.takeIf { it.isNotBlank() } ?: "مدرس المادة"
    val packageName = hw.packageName.orEmpty()
    val termTitle = hw.termTitle.orEmpty()
    val stateLabel = when (submissionState) {
        "NotSubmitted" -> "متبقي"
        "InProgress" -> "قيد الحل"
        "PendingReview" -> "قيد التصحيح"
        "Graded" -> "مصحح"
        "Missed" -> "فائت"
        else -> if (hw.isSubmitted) "مسلم" else "متبقي"
    }
    val stateColor = when (submissionState) {
        "Graded" -> BrandTeal
        "PendingReview", "InProgress" -> BrandWarmGold
        "Missed", "NotSubmitted" -> FailRed
        else -> if (hw.isSubmitted) BrandTeal else FailRed
    }
    Card(
        shape = RoundedCornerShape(12.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        modifier = Modifier.fillMaxWidth().clickable { expanded = !expanded }
    ) {
        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.SpaceBetween) {
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
                            text = "$teacherName • التقييم: ${hw.grade ?: "تم التسليم"}",
                            fontSize = 11.sp,
                            color = BrandTeal
                        )
                    } else {
                        Text(
                            text = "$teacherName • لم يتم التسليم",
                            fontSize = 11.sp,
                            color = FailRed
                        )
                    }
                    if (packageName.isNotBlank() || termTitle.isNotBlank() || hw.submittedAt != null) {
                        Text(
                            text = listOfNotNull(
                                packageName.takeIf { it.isNotBlank() },
                                termTitle.takeIf { it.isNotBlank() },
                                hw.submittedAt?.let { formatDisplayDateTime(it) }
                            ).joinToString(" • "),
                            fontSize = 11.sp,
                            color = TextSecondary
                        )
                    }
                }
                Box(
                    modifier = Modifier
                        .clip(RoundedCornerShape(8.dp))
                        .background(stateColor.copy(alpha = 0.1f))
                        .padding(horizontal = 12.dp, vertical = 6.dp),
                    contentAlignment = Alignment.Center
                ) {
                    Text(
                        text = stateLabel,
                        fontWeight = FontWeight.Bold,
                        fontSize = 12.sp,
                        color = stateColor
                    )
                }
            }
            if (expanded) {
                if (mistakes.isEmpty()) {
                    Text(if (hw.isSubmitted) "لا توجد أخطاء مسجلة في هذا الواجب." else "الواجب لم يتم حله بعد.", color = TextSecondary, fontSize = 12.sp)
                } else {
                    mistakes.forEachIndexed { index, mistake ->
                        ReviewMistakeBlock(
                            title = "سؤال ${index + 1}",
                            question = mistake.questionText,
                            studentAnswer = mistake.studentAnswer,
                            correctAnswer = mistake.correctAnswer,
                            correction = mistake.writtenCorrection,
                            score = "${mistake.scoreReceived ?: 0} / ${mistake.points}"
                        )
                    }
                }
            }
        }
    }
}

@Composable
fun ReviewMistakeBlock(
    title: String,
    question: String,
    studentAnswer: String,
    correctAnswer: String?,
    correction: String?,
    score: String
) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(10.dp))
            .background(MaterialTheme.colorScheme.background)
            .padding(12.dp),
        verticalArrangement = Arrangement.spacedBy(6.dp)
    ) {
        Text("$title • $score", fontWeight = FontWeight.Black, fontSize = 12.sp, color = BrandTeal)
        Text(question, fontSize = 12.sp, fontWeight = FontWeight.Bold, color = MaterialTheme.colorScheme.onSurface)
        Text("إجابة الطالب: $studentAnswer", fontSize = 12.sp, color = FailRed)
        correctAnswer?.takeIf { it.isNotBlank() }?.let {
            Text("الإجابة الصحيحة: $it", fontSize = 12.sp, color = PassGreen)
        }
        correction?.takeIf { it.isNotBlank() }?.let {
            Text("التصحيح: $it", fontSize = 12.sp, color = TextSecondary, lineHeight = 18.sp)
        }
    }
}

private fun formatDuration(seconds: Int): String {
    if (seconds <= 0) return "لم يبدأ بعد"
    val minutes = seconds / 60
    if (minutes <= 0) return "أقل من دقيقة"
    val hours = minutes / 60
    val remainingMinutes = minutes % 60
    return when {
        hours <= 0 -> "$minutes دقيقة"
        remainingMinutes == 0 -> "$hours ساعة"
        else -> "$hours ساعة و $remainingMinutes دقيقة"
    }
}

private fun formatDisplayDateTime(raw: String?): String {
    if (raw.isNullOrBlank()) return "غير محدد"
    val cairoZone = ZoneId.of("Africa/Cairo")
    val dateTime = runCatching { OffsetDateTime.parse(raw).atZoneSameInstant(cairoZone).toLocalDateTime() }
        .recoverCatching { LocalDateTime.parse(raw).atZone(cairoZone).toLocalDateTime() }
        .getOrNull()
        ?: return raw.take(16).replace('T', ' ')

    val date = dateTime.toLocalDate()
    val today = LocalDate.now(cairoZone)
    val time = dateTime.format(DateTimeFormatter.ofPattern("h:mm a", Locale("ar")))
    return when (date) {
        today -> "اليوم $time"
        today.minusDays(1) -> "أمس $time"
        else -> dateTime.format(DateTimeFormatter.ofPattern("d MMM yyyy - h:mm a", Locale("ar")))
    }
}

// Extension to allow custom opacity on Color variables
private fun Modifier.opacity(value: Float): Modifier = this
