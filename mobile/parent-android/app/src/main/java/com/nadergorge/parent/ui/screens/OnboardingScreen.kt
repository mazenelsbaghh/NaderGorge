package com.nadergorge.parent.ui.screens

import androidx.compose.animation.*
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowLeft
import androidx.compose.material.icons.filled.PlayArrow
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
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.nadergorge.parent.ui.theme.*

@Composable
fun OnboardingScreen(
    onStartTracking: () -> Unit,
    modifier: Modifier = Modifier
) {
    Box(
        modifier = modifier
            .fillMaxSize()
            .background(Color.White)
            .padding(24.dp)
    ) {
        // 1. Top Logo/Branding
        Text(
            text = "منصة مسار",
            fontSize = 18.sp,
            fontWeight = FontWeight.Black,
            color = BrandTeal,
            modifier = Modifier
                .align(Alignment.TopStart)
                .padding(top = 16.dp)
        )

        // 2. Large Motivational Header (Aligned Middle-Left)
        Column(
            modifier = Modifier
                .align(Alignment.CenterStart)
                .fillMaxWidth(0.85f)
                .offset(y = (-80).dp)
        ) {
            Text(
                text = "تابع مستوى\nابنك الأكاديمي\nفي مكان واحد",
                fontSize = 32.sp,
                lineHeight = 44.sp,
                fontWeight = FontWeight.ExtraBold,
                color = BrandDeepNavy,
                textAlign = TextAlign.Start
            )
            Spacer(modifier = Modifier.height(12.dp))
            Text(
                text = "رحلة تعليمية تبدأ بخطوة. تابع الدرجات والحضور والتنبيهات الإرشادية لحظة بلحظة.",
                fontSize = 14.sp,
                lineHeight = 22.sp,
                fontWeight = FontWeight.Normal,
                color = BrandDarkGray,
                textAlign = TextAlign.Start
            )
        }

        // 3. Center/Bottom Accent Graphic (Ascending Stairs & Glowing Path)
        Box(
            modifier = Modifier
                .fillMaxWidth()
                .height(260.dp)
                .align(Alignment.BottomCenter)
                .offset(y = (-110).dp)
        ) {
            Canvas(modifier = Modifier.fillMaxSize()) {
                val width = size.width
                val height = size.height

                // Draw background grids (dotted)
                val gridSpacing = 40f
                for (x in 0 until (width / gridSpacing).toInt()) {
                    for (y in 0 until (height / gridSpacing).toInt()) {
                        drawCircle(
                            color = BrandTeal.copy(alpha = 0.12f),
                            radius = 2f,
                            center = Offset(x * gridSpacing, y * gridSpacing)
                        )
                    }
                }

                // Draw stairs paths (ascending) representing Massar (learning steps)
                val path = Path().apply {
                    moveTo(width * 0.1f, height * 0.9f)
                    lineTo(width * 0.3f, height * 0.9f)
                    lineTo(width * 0.3f, height * 0.7f)
                    lineTo(width * 0.5f, height * 0.7f)
                    lineTo(width * 0.5f, height * 0.5f)
                    lineTo(width * 0.7f, height * 0.5f)
                    lineTo(width * 0.7f, height * 0.3f)
                    lineTo(width * 0.9f, height * 0.3f)
                }

                drawPath(
                    path = path,
                    color = BrandTeal.copy(alpha = 0.35f),
                    style = Stroke(width = 8f)
                )

                // Draw ascending stars/milestones in Gold
                drawCircle(
                    color = BrandWarmGold,
                    radius = 8f,
                    center = Offset(width * 0.3f, height * 0.7f)
                )
                drawCircle(
                    color = BrandWarmGold,
                    radius = 8f,
                    center = Offset(width * 0.5f, height * 0.5f)
                )
                drawCircle(
                    color = BrandWarmGold,
                    radius = 12f,
                    center = Offset(width * 0.7f, height * 0.3f)
                )

                // Draw glowing progress path
                val progressPath = Path().apply {
                    moveTo(width * 0.1f, height * 0.9f)
                    quadraticBezierTo(
                        width * 0.4f, height * 0.85f,
                        width * 0.7f, height * 0.3f
                    )
                }
                drawPath(
                    path = progressPath,
                    color = BrandTeal,
                    style = Stroke(width = 4f)
                )
            }
        }

        // 4. Bottom Custom Control Pill (resembling the slider in image)
        Row(
            modifier = Modifier
                .align(Alignment.BottomCenter)
                .padding(bottom = 24.dp)
                .fillMaxWidth()
                .height(64.dp)
                .clip(RoundedCornerShape(32.dp))
                .background(BrandDeepNavy)
                .clickable { onStartTracking() }
                .padding(horizontal = 8.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.SpaceBetween
        ) {
            // Muted back indicator
            Box(
                modifier = Modifier
                    .size(48.dp)
                    .clip(CircleShape)
                    .background(Color.White.copy(alpha = 0.1f)),
                contentAlignment = Alignment.Center
            ) {
                Icon(
                    imageVector = Icons.AutoMirrored.Filled.KeyboardArrowLeft,
                    contentDescription = null,
                    tint = Color.White.copy(alpha = 0.4f),
                    modifier = Modifier.size(24.dp)
                )
            }

            // Play CTA button & Start text
            Row(
                verticalAlignment = Alignment.CenterVertically,
                modifier = Modifier.weight(1f),
                horizontalArrangement = Arrangement.Center
            ) {
                Box(
                    modifier = Modifier
                        .size(48.dp)
                        .clip(CircleShape)
                        .background(BrandTeal),
                    contentAlignment = Alignment.Center
                ) {
                    Icon(
                        imageVector = Icons.Default.PlayArrow,
                        contentDescription = "ابدأ المتابعة",
                        tint = Color.White,
                        modifier = Modifier.size(24.dp)
                    )
                }
                Spacer(modifier = Modifier.width(16.dp))
                Text(
                    text = "ابدأ الآن",
                    fontSize = 16.sp,
                    fontWeight = FontWeight.Bold,
                    color = Color.White
                )
            }

            // Slider arrows decoration (representing ">>>")
            Row(
                modifier = Modifier.padding(end = 16.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = "•••",
                    fontSize = 18.sp,
                    letterSpacing = 2.sp,
                    color = Color.White.copy(alpha = 0.4f),
                    fontWeight = FontWeight.Black
                )
            }
        }
    }
}
