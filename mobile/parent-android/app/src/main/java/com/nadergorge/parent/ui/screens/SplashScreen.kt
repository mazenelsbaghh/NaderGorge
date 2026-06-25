package com.nadergorge.parent.ui.screens

import androidx.compose.animation.core.*
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.nadergorge.parent.ui.theme.*
import kotlinx.coroutines.delay

@Composable
fun SplashScreen(
    onFinished: () -> Unit,
    modifier: Modifier = Modifier
) {
    // Logo Float Animation
    val infiniteTransition = rememberInfiniteTransition(label = "logo_float")
    val floatOffset by infiniteTransition.animateFloat(
        initialValue = 0f,
        targetValue = -12f,
        animationSpec = infiniteRepeatable(
            animation = tween(durationMillis = 2000, easing = EaseInOutSine),
            repeatMode = RepeatMode.Reverse
        ),
        label = "float"
    )

    // Progress bar animation simulator
    var progress by remember { mutableStateOf(0f) }
    LaunchedEffect(Unit) {
        val animationSteps = 100
        for (i in 0..animationSteps) {
            progress = i / 100f
            delay(20) // total 2 seconds
        }
        onFinished()
    }

    Box(
        modifier = modifier
            .fillMaxSize()
            .background(
                Brush.verticalGradient(
                    colors = listOf(
                        MaterialTheme.colorScheme.background,
                        BrandTeal.copy(alpha = 0.08f),
                        BrandTeal.copy(alpha = 0.15f)
                    )
                )
            )
    ) {
        // Decorative Light Dots in corners
        Row(
            modifier = Modifier.fillMaxWidth().padding(24.dp),
            horizontalArrangement = Arrangement.SpaceBetween
        ) {
            CornerDotsLight()
            CornerDotsLight()
        }

        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(32.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.SpaceBetween
        ) {
            Spacer(modifier = Modifier.height(32.dp))

            // Branding Section
            Column(
                horizontalAlignment = Alignment.CenterHorizontally,
                verticalArrangement = Arrangement.Center
            ) {
                // Floating Brand SVG Logo Container
                Box(
                    contentAlignment = Alignment.Center,
                    modifier = Modifier
                        .offset(y = floatOffset.dp)
                        .size(128.dp)
                        .clip(RoundedCornerShape(32.dp))
                        .background(BrandTeal)
                        .padding(24.dp)
                ) {
                    Image(
                        painter = painterResource(id = com.nadergorge.parent.R.drawable.ic_logo_mark_light),
                        contentDescription = "Logo",
                        modifier = Modifier.fillMaxSize()
                    )
                }

                Spacer(modifier = Modifier.height(28.dp))

                Text(
                    text = "بوابة المتابعة",
                    fontSize = 26.sp,
                    fontWeight = FontWeight.ExtraBold,
                    color = BrandTeal,
                    textAlign = TextAlign.Center
                )

                Spacer(modifier = Modifier.height(8.dp))

                Text(
                    text = "منصة تعليمية متطورة لمتابعة الأداء الأكاديمي والنمو المعرفي",
                    fontSize = 14.sp,
                    lineHeight = 22.sp,
                    fontWeight = FontWeight.Normal,
                    color = Color.Gray,
                    textAlign = TextAlign.Center,
                    modifier = Modifier.fillMaxWidth(0.85f)
                )
            }

            // Progress & Footer Section
            Column(
                modifier = Modifier.fillMaxWidth().padding(bottom = 16.dp),
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                // Linear Progress bar
                LinearProgressIndicator(
                    progress = { progress },
                    color = BrandTeal,
                    trackColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.4f),
                    strokeCap = androidx.compose.ui.graphics.StrokeCap.Round,
                    modifier = Modifier
                        .fillMaxWidth(0.8f)
                        .height(6.dp)
                )

                Spacer(modifier = Modifier.height(12.dp))

                Text(
                    text = "جاري تحميل البيانات...",
                    fontSize = 12.sp,
                    fontWeight = FontWeight.Medium,
                    color = Color.Gray.copy(alpha = 0.8f)
                )
            }
        }
    }
}
