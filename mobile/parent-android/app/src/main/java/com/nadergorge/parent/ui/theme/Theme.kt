package com.nadergorge.parent.ui.theme

import android.app.Activity
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Typography
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.SideEffect
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.toArgb
import androidx.compose.ui.platform.LocalView
import androidx.compose.ui.text.font.Font
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.core.view.WindowCompat
import com.nadergorge.parent.R

val TajawalFontFamily = FontFamily(
    Font(R.font.tajawal_regular, FontWeight.Normal),
    Font(R.font.tajawal_medium, FontWeight.Medium),
    Font(R.font.tajawal_bold, FontWeight.Bold),
    Font(R.font.tajawal_black, FontWeight.Black)
)

private val defaultTypography = Typography()
val TajawalTypography = Typography(
    displayLarge = defaultTypography.displayLarge.copy(fontFamily = TajawalFontFamily),
    displayMedium = defaultTypography.displayMedium.copy(fontFamily = TajawalFontFamily),
    displaySmall = defaultTypography.displaySmall.copy(fontFamily = TajawalFontFamily),
    headlineLarge = defaultTypography.headlineLarge.copy(fontFamily = TajawalFontFamily),
    headlineMedium = defaultTypography.headlineMedium.copy(fontFamily = TajawalFontFamily),
    headlineSmall = defaultTypography.headlineSmall.copy(fontFamily = TajawalFontFamily),
    titleLarge = defaultTypography.titleLarge.copy(fontFamily = TajawalFontFamily),
    titleMedium = defaultTypography.titleMedium.copy(fontFamily = TajawalFontFamily),
    titleSmall = defaultTypography.titleSmall.copy(fontFamily = TajawalFontFamily),
    bodyLarge = defaultTypography.bodyLarge.copy(fontFamily = TajawalFontFamily),
    bodyMedium = defaultTypography.bodyMedium.copy(fontFamily = TajawalFontFamily),
    bodySmall = defaultTypography.bodySmall.copy(fontFamily = TajawalFontFamily),
    labelLarge = defaultTypography.labelLarge.copy(fontFamily = TajawalFontFamily),
    labelMedium = defaultTypography.labelMedium.copy(fontFamily = TajawalFontFamily),
    labelSmall = defaultTypography.labelSmall.copy(fontFamily = TajawalFontFamily)
)

private val DarkColorScheme = darkColorScheme(
    primary = AccentTeal,
    secondary = BrandWarmGold,
    tertiary = BrandTeal,
    background = DarkPrimary,
    surface = SurfaceCard,
    onPrimary = Color.White,
    onBackground = TextPrimary,
    onSurface = TextPrimary
)

private val LightColorScheme = lightColorScheme(
    primary = BrandTeal,
    secondary = BrandWarmGold,
    tertiary = BrandDeepNavy,
    background = LightPrimary,
    surface = LightSurfaceCard,
    onPrimary = Color.White,
    onBackground = LightTextPrimary,
    onSurface = LightTextPrimary
)

@Composable
fun ParentAppTheme(
    darkTheme: Boolean = isSystemInDarkTheme(),
    content: @Composable () -> Unit
) {
    val colorScheme = if (darkTheme) DarkColorScheme else LightColorScheme

    MaterialTheme(
        colorScheme = colorScheme,
        typography = TajawalTypography,
        content = content
    )
}

