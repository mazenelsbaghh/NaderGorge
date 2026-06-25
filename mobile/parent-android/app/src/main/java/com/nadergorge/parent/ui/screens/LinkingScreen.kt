package com.nadergorge.parent.ui.screens

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Person
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.ArrowBack
import androidx.compose.material.icons.filled.ExitToApp
import androidx.compose.material.icons.filled.Info
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.platform.LocalLayoutDirection
import androidx.compose.ui.unit.LayoutDirection
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.nadergorge.parent.data.storage.LinkedStudent
import com.nadergorge.parent.data.api.StudentDetailsResponse
import com.nadergorge.parent.ui.theme.*

@Composable
fun LinkingScreen(
    viewModel: LinkingViewModel,
    onSuccess: () -> Unit,
    modifier: Modifier = Modifier
) {
    val uiState by viewModel.uiState.collectAsState()
    val code by viewModel.code.collectAsState()

    LaunchedEffect(uiState) {
        if (uiState is LinkingUiState.Success) {
            onSuccess()
            viewModel.resetState()
        }
    }

    when (val state = uiState) {
        is LinkingUiState.Review -> {
            StudentConfirmationScreen(
                student = state.student,
                details = state.details,
                onConfirm = { viewModel.confirmLink(state.student) },
                onCancel = { viewModel.cancelLink() },
                modifier = modifier
            )
        }
        else -> {
            Column(
                modifier = modifier
                    .fillMaxSize()
                    .background(Color.White)
            ) {
                // 1. Curved Top Header Card
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(220.dp)
                        .clip(RoundedCornerShape(bottomStart = 32.dp, bottomEnd = 32.dp))
                        .background(BrandTeal)
                ) {
                    // Background Dot Pattern Mockup
                    Box(modifier = Modifier.fillMaxSize().padding(16.dp)) {
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.SpaceBetween
                        ) {
                            CornerDotsLight()
                            CornerDotsLight()
                        }
                    }

                    Column(
                        modifier = Modifier.fillMaxSize(),
                        horizontalAlignment = Alignment.CenterHorizontally,
                        verticalArrangement = Arrangement.Center
                    ) {
                        // Circle housing the brand SVG logo
                        Box(
                            contentAlignment = Alignment.Center,
                            modifier = Modifier
                                .size(72.dp)
                                .clip(CircleShape)
                                .background(Color.White.copy(alpha = 0.15f))
                        ) {
                            Image(
                                painter = painterResource(id = com.nadergorge.parent.R.drawable.ic_logo_mark_light),
                                contentDescription = "Logo",
                                modifier = Modifier.size(44.dp)
                            )
                        }

                        Spacer(modifier = Modifier.height(12.dp))

                        Text(
                            text = "بوابة المتابعة",
                            fontSize = 22.sp,
                            fontWeight = FontWeight.Black,
                            color = Color.White
                        )
                        Text(
                            text = "نظام متابعة الطلاب الأكاديمي",
                            fontSize = 12.sp,
                            fontWeight = FontWeight.Medium,
                            color = Color.White.copy(alpha = 0.8f)
                        )
                    }
                }

                // 2. White Main Content Box (Forms + Keypad)
                Column(
                    modifier = Modifier
                        .fillMaxWidth()
                        .weight(1f)
                        .padding(horizontal = 24.dp, vertical = 16.dp),
                    horizontalAlignment = Alignment.CenterHorizontally,
                    verticalArrangement = Arrangement.SpaceBetween
                ) {
                    Card(
                        shape = RoundedCornerShape(20.dp),
                        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                        elevation = CardDefaults.cardElevation(defaultElevation = 2.dp),
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Column(
                            modifier = Modifier.padding(20.dp),
                            horizontalAlignment = Alignment.CenterHorizontally
                        ) {
                            Text(
                                text = "تسجيل الدخول - عصري",
                                fontSize = 18.sp,
                                fontWeight = FontWeight.Black,
                                color = BrandDeepNavy,
                                modifier = Modifier.padding(bottom = 4.dp)
                            )

                            Text(
                                text = "يرجى إدخال رقم المتابعة الخاص بالطالب",
                                fontSize = 13.sp,
                                fontWeight = FontWeight.Medium,
                                color = Color.Gray,
                                modifier = Modifier.padding(bottom = 16.dp)
                            )

                            // Display Input Field
                            Row(
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .height(54.dp)
                                    .clip(RoundedCornerShape(12.dp))
                                    .background(MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.3f))
                                    .padding(horizontal = 16.dp),
                                verticalAlignment = Alignment.CenterVertically,
                                horizontalArrangement = Arrangement.Start
                            ) {
                                Icon(
                                    imageVector = Icons.Default.Person,
                                    contentDescription = null,
                                    tint = BrandTeal,
                                    modifier = Modifier.size(24.dp)
                                )
                                Spacer(modifier = Modifier.width(16.dp))
                                Text(
                                    text = if (code.isEmpty()) "مثال: 123456" else code,
                                    fontSize = 18.sp,
                                    fontWeight = FontWeight.Bold,
                                    letterSpacing = if (code.isEmpty()) 0.sp else 4.sp,
                                    color = if (code.isEmpty()) Color.Gray.copy(alpha = 0.7f) else BrandDeepNavy
                                )
                            }

                            Spacer(modifier = Modifier.height(16.dp))

                            // Custom Keypad
                            NumericKeypad(
                                onDigitClick = { digit ->
                                    if (code.length < 6) {
                                        viewModel.onCodeChange(code + digit)
                                    }
                                },
                                onBackspaceClick = {
                                    if (code.isNotEmpty()) {
                                        viewModel.onCodeChange(code.dropLast(1))
                                    }
                                },
                                onClearClick = {
                                    viewModel.onCodeChange("")
                                }
                            )

                            Spacer(modifier = Modifier.height(16.dp))

                            if (uiState is LinkingUiState.Error) {
                                Text(
                                    text = (uiState as LinkingUiState.Error).message,
                                    color = MaterialTheme.colorScheme.error,
                                    style = MaterialTheme.typography.bodySmall,
                                    modifier = Modifier.padding(bottom = 12.dp),
                                    textAlign = TextAlign.Center
                                )
                            }

                            // Login Action Button
                            Button(
                                onClick = {
                                    if (uiState is LinkingUiState.Error) {
                                        viewModel.resetState()
                                    } else {
                                        viewModel.linkStudent("mock_device_token")
                                    }
                                },
                                colors = ButtonDefaults.buttonColors(
                                    containerColor = if (uiState is LinkingUiState.Error) MaterialTheme.colorScheme.error else BrandTeal
                                ),
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .height(50.dp)
                                    .clip(RoundedCornerShape(12.dp)),
                                enabled = (code.length == 6 || uiState is LinkingUiState.Error) && uiState !is LinkingUiState.Loading
                            ) {
                                if (uiState is LinkingUiState.Loading) {
                                    CircularProgressIndicator(
                                        color = Color.White,
                                        modifier = Modifier.size(24.dp)
                                    )
                                } else {
                                    Row(
                                        verticalAlignment = Alignment.CenterVertically,
                                        horizontalArrangement = Arrangement.Center
                                    ) {
                                        Text(
                                            text = if (uiState is LinkingUiState.Error) "إعادة المحاولة" else "تأكيد المتابعة",
                                            fontSize = 16.sp,
                                            fontWeight = FontWeight.Bold,
                                            color = Color.White
                                        )
                                        Spacer(modifier = Modifier.width(8.dp))
                                        Icon(
                                            imageVector = if (uiState is LinkingUiState.Error) Icons.Default.ArrowBack else Icons.Default.ExitToApp,
                                            contentDescription = null,
                                            tint = Color.White,
                                            modifier = Modifier.size(20.dp)
                                        )
                                    }
                                }
                            }

                            Spacer(modifier = Modifier.height(12.dp))
                            Text(text = "أو", fontSize = 13.sp, fontWeight = FontWeight.Bold, color = Color.Gray)
                            Spacer(modifier = Modifier.height(12.dp))

                            // QR Scanner Button
                            OutlinedButton(
                                onClick = { /* Mock scan code */ },
                                shape = RoundedCornerShape(12.dp),
                                colors = ButtonDefaults.outlinedButtonColors(contentColor = BrandTeal),
                                modifier = Modifier.fillMaxWidth().height(50.dp)
                            ) {
                                Row(
                                    verticalAlignment = Alignment.CenterVertically,
                                    horizontalArrangement = Arrangement.Center
                                ) {
                                    Text(
                                        text = "Scan QR Code",
                                        fontSize = 16.sp,
                                        fontWeight = FontWeight.Bold
                                    )
                                    Spacer(modifier = Modifier.width(8.dp))
                                    Icon(
                                        imageVector = Icons.Default.Add,
                                        contentDescription = null,
                                        modifier = Modifier.size(20.dp)
                                    )
                                }
                            }
                        }
                    }

                    // 3. Support & Copyright footer
                    Column(
                        horizontalAlignment = Alignment.CenterHorizontally,
                        verticalArrangement = Arrangement.Center,
                        modifier = Modifier.padding(top = 16.dp, bottom = 8.dp)
                    ) {
                        Text(
                            text = "تواجه مشكلة في الدخول؟",
                            fontSize = 13.sp,
                            color = Color.Gray
                        )
                        Text(
                            text = "تواصل مع الدعم الفني",
                            fontSize = 13.sp,
                            fontWeight = FontWeight.Bold,
                            color = BrandTeal,
                            modifier = Modifier.clickable { /* Contact support */ }
                        )
                        Spacer(modifier = Modifier.height(12.dp))
                        Text(
                            text = "© ٢٠٢٤ جميع الحقوق محفوظة لشركة حلول التعليم",
                            fontSize = 11.sp,
                            color = Color.Gray.copy(alpha = 0.7f)
                        )
                    }
                }
            }
        }
    }
}

@Composable
fun StudentConfirmationScreen(
    student: LinkedStudent,
    details: StudentDetailsResponse,
    onConfirm: () -> Unit,
    onCancel: () -> Unit,
    modifier: Modifier = Modifier
) {
    val stageName = when {
        details.grade.contains("Baccalaureate", true) || details.grade.contains("ثانوي", true) -> "المرحلة الثانوية"
        details.grade.contains("Medium", true) || details.grade.contains("متوسط", true) -> "المرحلة المتوسطة"
        else -> "المرحلة الدراسية"
    }

    Column(
        modifier = modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background)
    ) {
        // 1. Top Bar
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .height(64.dp)
                .background(MaterialTheme.colorScheme.surface)
                .padding(horizontal = 16.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.SpaceBetween
        ) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(
                    imageVector = Icons.Default.Person,
                    contentDescription = null,
                    tint = BrandTeal,
                    modifier = Modifier.size(24.dp)
                )
                Spacer(modifier = Modifier.width(8.dp))
                Text(
                    text = "بوابة المتابعة",
                    fontSize = 18.sp,
                    fontWeight = FontWeight.Bold,
                    color = BrandTeal
                )
            }
            IconButton(onClick = onCancel) {
                Icon(
                    imageVector = Icons.Default.ArrowBack,
                    contentDescription = "الرجوع",
                    tint = MaterialTheme.colorScheme.onSurface
                )
            }
        }

        // 2. Main Content
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .weight(1f)
                .padding(24.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.SpaceBetween
        ) {
            // Header
            Column(
                horizontalAlignment = Alignment.CenterHorizontally,
                verticalArrangement = Arrangement.Center,
                modifier = Modifier.padding(bottom = 12.dp)
            ) {
                Text(
                    text = "تأكيد الربط - مراجعة البيانات",
                    fontSize = 20.sp,
                    fontWeight = FontWeight.Black,
                    color = MaterialTheme.colorScheme.onSurface,
                    textAlign = TextAlign.Center
                )
                Spacer(modifier = Modifier.height(4.dp))
                Text(
                    text = "يرجى التأكد من هوية الطالب المعروض أدناه لإتمام عملية الربط التعليمي.",
                    fontSize = 13.sp,
                    color = Color.Gray,
                    textAlign = TextAlign.Center,
                    modifier = Modifier.fillMaxWidth(0.95f)
                )
            }

            // Student profile card
            Card(
                shape = RoundedCornerShape(20.dp),
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                elevation = CardDefaults.cardElevation(defaultElevation = 2.dp),
                modifier = Modifier.fillMaxWidth()
            ) {
                Column(
                    modifier = Modifier.fillMaxWidth()
                ) {
                    // Accent banner
                    Box(
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(80.dp)
                            .background(BrandTeal)
                    ) {
                        Box(modifier = Modifier.fillMaxSize().padding(8.dp)) {
                            CornerDotsLight()
                        }
                    }

                    // Avatar & Details
                    Column(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(horizontal = 20.dp, vertical = 16.dp)
                            .offset(y = (-40).dp),
                        horizontalAlignment = Alignment.CenterHorizontally
                    ) {
                        // Profile Avatar
                        Box(
                            contentAlignment = Alignment.Center,
                            modifier = Modifier
                                .size(80.dp)
                                .clip(CircleShape)
                                .background(MaterialTheme.colorScheme.surfaceVariant)
                                .border(4.dp, MaterialTheme.colorScheme.surface, CircleShape)
                        ) {
                            Text(
                                text = details.studentName.firstOrNull()?.toString() ?: "ط",
                                fontSize = 32.sp,
                                fontWeight = FontWeight.Bold,
                                color = BrandTeal
                            )
                        }

                        Spacer(modifier = Modifier.height(12.dp))

                        // Student name & Code
                        Text(
                            text = details.studentName,
                            fontSize = 18.sp,
                            fontWeight = FontWeight.Black,
                            color = BrandDeepNavy,
                            textAlign = TextAlign.Center
                        )

                        Row(
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.Center,
                            modifier = Modifier.padding(top = 4.dp)
                        ) {
                            Icon(
                                imageVector = Icons.Default.Person,
                                contentDescription = null,
                                tint = BrandTeal,
                                modifier = Modifier.size(16.dp)
                            )
                            Spacer(modifier = Modifier.width(4.dp))
                            Text(
                                text = "رقم المتابعة: ${student.studentId}",
                                fontSize = 13.sp,
                                fontWeight = FontWeight.Medium,
                                color = BrandTeal
                            )
                        }

                        Spacer(modifier = Modifier.height(24.dp))

                        // Details Grid
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.spacedBy(12.dp)
                        ) {
                            DetailCard(
                                label = "المرحلة الدراسية",
                                value = stageName,
                                modifier = Modifier.weight(1f)
                            )
                            DetailCard(
                                label = "الصف الدراسي",
                                value = details.grade,
                                modifier = Modifier.weight(1f)
                            )
                        }
                        Spacer(modifier = Modifier.height(12.dp))
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.spacedBy(12.dp)
                        ) {
                            DetailCard(
                                label = "الفصل الدراسي",
                                value = "1 / أ", // Mock as not in api response
                                modifier = Modifier.weight(1f)
                            )
                            DetailCard(
                                label = "المدرسة",
                                value = details.school,
                                modifier = Modifier.weight(1f)
                            )
                        }
                    }
                }
            }

            // Warning Box & Actions
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(top = 16.dp),
                verticalArrangement = Arrangement.spacedBy(16.dp)
            ) {
                // Warning
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .clip(RoundedCornerShape(12.dp))
                        .background(BrandWarmGold.copy(alpha = 0.15f))
                        .border(1.dp, BrandWarmGold.copy(alpha = 0.3f), RoundedCornerShape(12.dp))
                        .padding(16.dp),
                    verticalAlignment = Alignment.Top
                ) {
                    Icon(
                        imageVector = Icons.Default.Info,
                        contentDescription = null,
                        tint = BrandWarmGold,
                        modifier = Modifier.size(20.dp).offset(y = 2.dp)
                    )
                    Spacer(modifier = Modifier.width(12.dp))
                    Text(
                        text = "بمجرد التأكيد، ستتمكن من الوصول الكامل إلى الدرجات، السلوك، والجدول المدرسي الخاص بالطالب.",
                        fontSize = 12.sp,
                        fontWeight = FontWeight.Medium,
                        lineHeight = 18.sp,
                        color = BrandWarmGold
                    )
                }

                // Buttons
                Column(
                    verticalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    Button(
                        onClick = onConfirm,
                        colors = ButtonDefaults.buttonColors(containerColor = BrandTeal),
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(54.dp)
                            .clip(RoundedCornerShape(12.dp))
                    ) {
                        Text(
                            text = "نعم، هذا طفلي",
                            fontSize = 16.sp,
                            fontWeight = FontWeight.Bold,
                            color = Color.White
                        )
                    }

                    OutlinedButton(
                        onClick = onCancel,
                        shape = RoundedCornerShape(12.dp),
                        colors = ButtonDefaults.outlinedButtonColors(contentColor = Color.Gray),
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(54.dp)
                    ) {
                        Text(
                            text = "ليس طفلي / بيانات خاطئة",
                            fontSize = 16.sp,
                            fontWeight = FontWeight.Bold,
                            color = Color.Gray
                        )
                    }
                }
            }
        }
    }
}

@Composable
fun DetailCard(
    label: String,
    value: String,
    modifier: Modifier = Modifier
) {
    Card(
        shape = RoundedCornerShape(8.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.3f)),
        modifier = modifier
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(12.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Text(
                text = label,
                fontSize = 11.sp,
                color = Color.Gray,
                textAlign = TextAlign.Center
            )
            Spacer(modifier = Modifier.height(4.dp))
            Text(
                text = value,
                fontSize = 13.sp,
                fontWeight = FontWeight.Bold,
                color = MaterialTheme.colorScheme.onSurface,
                textAlign = TextAlign.Center
            )
        }
    }
}

@Composable
fun NumericKeypad(
    onDigitClick: (String) -> Unit,
    onBackspaceClick: () -> Unit,
    onClearClick: () -> Unit,
    modifier: Modifier = Modifier
) {
    CompositionLocalProvider(LocalLayoutDirection provides LayoutDirection.Ltr) {
        Column(
            modifier = modifier.fillMaxWidth(),
            verticalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            val keys = listOf(
                listOf("1", "2", "3"),
                listOf("4", "5", "6"),
                listOf("7", "8", "9")
            )

            for (rowIndex in keys.indices) {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    for (colIndex in keys[rowIndex].indices) {
                        val digit = keys[rowIndex][colIndex]
                        KeyButton(
                            text = digit,
                            onClick = { onDigitClick(digit) },
                            modifier = Modifier.weight(1f)
                        )
                    }
                }
            }
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                // Clear button
                KeyButton(
                    text = "مسح",
                    onClick = onClearClick,
                    modifier = Modifier.weight(1f)
                )
                // Zero button
                KeyButton(
                    text = "0",
                    onClick = { onDigitClick("0") },
                    modifier = Modifier.weight(1f)
                )
                // Backspace button
                KeyButton(
                    icon = Icons.Default.ArrowBack,
                    onClick = onBackspaceClick,
                    modifier = Modifier.weight(1f)
                )
            }
        }
    }
}

@Composable
fun KeyButton(
    text: String? = null,
    icon: ImageVector? = null,
    onClick: () -> Unit,
    modifier: Modifier = Modifier
) {
    Card(
        shape = RoundedCornerShape(12.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f)),
        modifier = modifier
            .height(54.dp)
            .clickable { onClick() }
    ) {
        Box(
            contentAlignment = Alignment.Center,
            modifier = Modifier.fillMaxSize()
        ) {
            if (text != null) {
                Text(
                    text = text,
                    fontSize = 18.sp,
                    fontWeight = FontWeight.Bold,
                    color = MaterialTheme.colorScheme.onSurface
                )
            } else if (icon != null) {
                Icon(
                    imageVector = icon,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.onSurface,
                    modifier = Modifier.size(24.dp)
                )
            }
        }
    }
}

@Composable
fun CornerDotsLight(modifier: Modifier = Modifier) {
    Canvas(modifier = modifier.size(40.dp)) {
        val rows = 4
        val cols = 4
        val spacing = 10.dp.toPx()
        for (r in 0 until rows) {
            for (c in 0 until cols) {
                drawCircle(
                    color = Color.White.copy(alpha = 0.25f),
                    radius = 2.dp.toPx(),
                    center = Offset(c.toFloat() * spacing, r.toFloat() * spacing)
                )
            }
        }
    }
}

