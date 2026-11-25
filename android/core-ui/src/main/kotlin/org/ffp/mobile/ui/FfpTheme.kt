package org.ffp.mobile.ui

import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Shapes
import androidx.compose.material3.Typography
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

internal val FfpPrimary = Color(0xFF0755A5)
internal val FfpDarkBlue = Color(0xFF073675)
internal val FfpNavy = Color(0xFF09265D)
internal val FfpGold = Color(0xFFFFC400)
internal val FfpPale = Color(0xFFF3F7FC)
internal val FfpWhite = Color.White
internal val FfpText = Color(0xFF182A44)
internal val FfpMuted = Color(0xFF607087)
internal val FfpError = Color(0xFFB42318)
internal val FfpSuccess = Color(0xFF15803D)
internal val FfpSoftBlue = Color(0xFFDFEBF8)

internal object FfpSpace {
    val xSmall = 4.dp
    val small = 8.dp
    val medium = 12.dp
    val large = 16.dp
    val xLarge = 24.dp
    val section = 32.dp
    val panel = 40.dp
    val touch = 48.dp
    val action = 56.dp
    val cardElevation = 6.dp
}

private val colors = lightColorScheme(
    primary = FfpPrimary,
    onPrimary = FfpWhite,
    secondary = FfpDarkBlue,
    secondaryContainer = FfpSoftBlue,
    onSecondaryContainer = FfpNavy,
    tertiary = FfpGold,
    onTertiary = FfpNavy,
    background = FfpPale,
    onBackground = FfpText,
    surface = FfpWhite,
    onSurface = FfpText,
    onSurfaceVariant = FfpMuted,
    error = FfpError,
)

private val typography = Typography(
    displaySmall = TextStyle(fontSize = 38.sp, lineHeight = 44.sp, fontWeight = FontWeight.Bold),
    headlineMedium = TextStyle(fontSize = 30.sp, lineHeight = 36.sp, fontWeight = FontWeight.Bold),
    titleLarge = TextStyle(fontSize = 22.sp, lineHeight = 28.sp, fontWeight = FontWeight.SemiBold),
    titleMedium = TextStyle(fontSize = 16.sp, lineHeight = 22.sp, fontWeight = FontWeight.SemiBold),
)

@Composable
fun FfpAppTheme(content: @Composable () -> Unit) {
    MaterialTheme(
        colorScheme = colors,
        typography = typography,
        shapes = Shapes(
            small = RoundedCornerShape(10.dp),
            medium = RoundedCornerShape(14.dp),
            large = RoundedCornerShape(24.dp),
        ),
        content = content,
    )
}
