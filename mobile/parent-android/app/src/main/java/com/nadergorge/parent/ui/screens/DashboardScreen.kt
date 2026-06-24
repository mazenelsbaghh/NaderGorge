package com.nadergorge.parent.ui.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.ArrowDropDown
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
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

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Text(
                            text = activeStudent?.name ?: "متابعة أولياء الأمور",
                            style = MaterialTheme.typography.titleLarge.copy(fontWeight = FontWeight.Bold),
                            modifier = Modifier.weight(1f)
                        )
                        IconButton(onClick = { dropdownExpanded = true }) {
                            Icon(Icons.Default.ArrowDropDown, contentDescription = "تبديل الطالب")
                        }
                        DropdownMenu(
                            expanded = dropdownExpanded,
                            onDismissRequest = { dropdownExpanded = false }
                        ) {
                            linkedStudents.forEach { student ->
                                DropdownMenuItem(
                                    text = { Text(student.name) },
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
                            if (activeStudent != null) {
                                DropdownMenuItem(
                                    text = {
                                        Row(verticalAlignment = Alignment.CenterVertically) {
                                            Icon(
                                                Icons.Default.Delete,
                                                contentDescription = null,
                                                tint = MaterialTheme.colorScheme.error,
                                                modifier = Modifier.size(16.dp)
                                            )
                                            Spacer(modifier = Modifier.width(8.dp))
                                            Text("إلغاء ربط الطالب الحالي", color = MaterialTheme.colorScheme.error)
                                        }
                                    },
                                    onClick = {
                                        dropdownExpanded = false
                                        activeStudent?.let { viewModel.removeStudent(it.studentId) }
                                    }
                                )
                            }
                        }
                    }
                },
                actions = {
                    if (activeStudent != null) {
                        IconButton(onClick = { viewModel.fetchStudentDetails(activeStudent!!.studentId) }) {
                            Icon(Icons.Default.Refresh, contentDescription = "تحديث")
                        }
                    }
                }
            )
        }
    ) { innerPadding ->
        Box(
            modifier = modifier
                .fillMaxSize()
                .padding(innerPadding)
                .background(MaterialTheme.colorScheme.background)
        ) {
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
                        CircularProgressIndicator(color = AccentTeal)
                    }
                }
                is DashboardUiState.Success -> {
                    StudentDetailsView(details = state.details)
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
        }
    }
}

@Composable
fun StudentDetailsView(details: StudentDetailsResponse) {
    LazyColumn(
        modifier = Modifier.fillMaxSize(),
        contentPadding = PaddingValues(16.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp)
    ) {
        // School & Grade Card
        item {
            Card(
                shape = RoundedCornerShape(12.dp),
                colors = CardDefaults.cardColors(containerColor = SurfaceCard),
                modifier = Modifier.fillMaxWidth()
            ) {
                Column(modifier = Modifier.padding(16.dp)) {
                    Text(
                        text = details.studentName,
                        style = MaterialTheme.typography.headlineSmall.copy(fontWeight = FontWeight.Bold),
                        color = TextPrimary
                    )
                    Spacer(modifier = Modifier.height(4.dp))
                    Text(
                        text = "${details.grade} • ${details.school}",
                        style = MaterialTheme.typography.bodyMedium,
                        color = TextSecondary
                    )
                }
            }
        }

        // Attendance Progress Card
        item {
            Card(
                shape = RoundedCornerShape(12.dp),
                colors = CardDefaults.cardColors(containerColor = SurfaceCard),
                modifier = Modifier.fillMaxWidth()
            ) {
                Row(
                    modifier = Modifier.padding(16.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Column(modifier = Modifier.weight(1f)) {
                        Text(
                            text = "نسبة حضور الفيديوهات والمحاضرات",
                            style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                            color = TextPrimary
                        )
                        Spacer(modifier = Modifier.height(8.dp))
                        Text(
                            text = "شاهد ${details.attendance.watchedLessons} من أصل ${details.attendance.totalLessons} محاضرة",
                            style = MaterialTheme.typography.bodyMedium,
                            color = TextSecondary
                        )
                    }
                    Box(contentAlignment = Alignment.Center) {
                        CircularProgressIndicator(
                            progress = (details.attendance.completionRate / 100f).toFloat(),
                            color = AccentTeal,
                            trackColor = MaterialTheme.colorScheme.background,
                            strokeWidth = 6.dp,
                            modifier = Modifier.size(70.dp)
                        )
                        Text(
                            text = "${details.attendance.completionRate.toInt()}%",
                            style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.Bold),
                            color = TextPrimary
                        )
                    }
                }
            }
        }

        // Warnings Section
        if (details.warnings.isNotEmpty()) {
            item {
                Text(
                    text = "التنبيهات الأكاديمية",
                    style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                    color = TextPrimary,
                    modifier = Modifier.padding(vertical = 4.dp)
                )
            }
            items(details.warnings) { warning ->
                WarningItem(warning)
            }
        }

        // Exams Section
        if (details.exams.isNotEmpty()) {
            item {
                Text(
                    text = "الامتحانات والتقييمات",
                    style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                    color = TextPrimary,
                    modifier = Modifier.padding(vertical = 4.dp)
                )
            }
            items(details.exams) { exam ->
                ExamItem(exam)
            }
        }

        // Homework Section
        if (details.homeworks.isNotEmpty()) {
            item {
                Text(
                    text = "الواجبات المنزلية",
                    style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                    color = TextPrimary,
                    modifier = Modifier.padding(vertical = 4.dp)
                )
            }
            items(details.homeworks) { homework ->
                HomeworkItem(homework)
            }
        }
    }
}

@Composable
fun WarningItem(warning: WarningInfo) {
    val severityColor = when (warning.severity.lowercase()) {
        "high" -> WarningHigh
        "medium" -> WarningMedium
        else -> WarningLow
    }
    
    val severityText = when (warning.severity.lowercase()) {
        "high" -> "هام جداً"
        "medium" -> "متوسط"
        else -> "تنبيه"
    }

    Card(
        shape = RoundedCornerShape(8.dp),
        colors = CardDefaults.cardColors(containerColor = SurfaceCard),
        modifier = Modifier.fillMaxWidth()
    ) {
        Column(modifier = Modifier.padding(12.dp)) {
            Row(
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.SpaceBetween,
                modifier = Modifier.fillMaxWidth()
            ) {
                Text(
                    text = warning.reason,
                    style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.SemiBold),
                    color = TextPrimary,
                    modifier = Modifier.weight(1f)
                )
                Spacer(modifier = Modifier.width(8.dp))
                SuggestionChip(
                    onClick = {},
                    label = { Text(severityText, fontSize = 11.sp) },
                    colors = SuggestionChipDefaults.suggestionChipColors(
                        labelColor = severityColor
                    )
                )
            }
            Spacer(modifier = Modifier.height(4.dp))
            Text(
                text = warning.createdAt.take(10), // Simple date substring
                style = MaterialTheme.typography.bodySmall,
                color = TextSecondary
            )
        }
    }
}

@Composable
fun ExamItem(exam: ExamInfo) {
    val isPassed = exam.status.lowercase() == "passed"
    val statusColor = if (isPassed) PassGreen else FailRed
    val statusText = if (isPassed) "ناجح" else "راسب"

    Card(
        shape = RoundedCornerShape(8.dp),
        colors = CardDefaults.cardColors(containerColor = SurfaceCard),
        modifier = Modifier.fillMaxWidth()
    ) {
        Row(
            modifier = Modifier.padding(12.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = exam.examTitle,
                    style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.SemiBold),
                    color = TextPrimary
                )
                Spacer(modifier = Modifier.height(4.dp))
                Text(
                    text = "الدرجة: ${exam.score} / ${exam.totalScore} (${exam.percentage.toInt()}%)",
                    style = MaterialTheme.typography.bodySmall,
                    color = TextSecondary
                )
            }
            Spacer(modifier = Modifier.width(8.dp))
            Text(
                text = statusText,
                style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.Bold),
                color = statusColor,
                modifier = Modifier.padding(horizontal = 8.dp)
            )
        }
    }
}

@Composable
fun HomeworkItem(homework: HomeworkInfo) {
    val statusColor = if (homework.isSubmitted) PassGreen else FailRed
    val statusText = if (homework.isSubmitted) "تم التسليم" else "لم يسلم"

    Card(
        shape = RoundedCornerShape(8.dp),
        colors = CardDefaults.cardColors(containerColor = SurfaceCard),
        modifier = Modifier.fillMaxWidth()
    ) {
        Row(
            modifier = Modifier.padding(12.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = homework.title,
                    style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.SemiBold),
                    color = TextPrimary
                )
                Spacer(modifier = Modifier.height(4.dp))
                val extraText = if (homework.isSubmitted && homework.grade != null) {
                    "التقييم: ${homework.grade} • تاريخ التسليم: ${homework.submittedAt ?: ""}"
                } else {
                    "لم يتم تقديم الواجب بعد"
                }
                Text(
                    text = extraText,
                    style = MaterialTheme.typography.bodySmall,
                    color = TextSecondary
                )
            }
            Spacer(modifier = Modifier.width(8.dp))
            Text(
                text = statusText,
                style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.Bold),
                color = statusColor,
                modifier = Modifier.padding(horizontal = 8.dp)
            )
        }
    }
}
