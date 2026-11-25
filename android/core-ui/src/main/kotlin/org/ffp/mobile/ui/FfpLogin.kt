package org.ffp.mobile.ui

import android.content.res.Configuration
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.text.selection.SelectionContainer
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.input.VisualTransformation
import androidx.compose.ui.unit.dp

@Composable
internal fun FfpLoginScaffold(
    profile: MobileProfile,
    online: Boolean,
    configured: Boolean,
    auth: LoginState,
    onSignIn: (String, String) -> Unit,
    openSettings: () -> Unit,
    openPreview: () -> Unit,
    onForgotPassword: () -> Unit,
    modeToggle: (() -> Unit)?,
    mock: Boolean,
    enrollTablet: (() -> Unit)?,
) {
    val landscape = LocalConfiguration.current.orientation == Configuration.ORIENTATION_LANDSCAPE
    BoxWithConstraints(Modifier.fillMaxSize().background(FfpPale)) {
        if (landscape && maxWidth >= 720.dp) {
            Row(Modifier.fillMaxSize()) {
                Box(Modifier.weight(0.54f).fillMaxSize().background(Brush.linearGradient(
                    listOf(FfpNavy, FfpDarkBlue, FfpPrimary),
                ))) {
                    BrandGeometry(Modifier.matchParentSize())
                    Column(
                        Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(FfpSpace.panel),
                        verticalArrangement = Arrangement.spacedBy(FfpSpace.xLarge),
                    ) {
                        FfpBrandHeader(profile)
                        FfpFeatureList(profile.capabilities)
                        FfpOfflineBanner(online, onBrandBackground = true)
                        FfpSyncStatus("Unavailable", FfpWhite)
                        Text(
                            "${profile.subtitle} photo pending",
                            Modifier.fillMaxWidth().border(1.dp, FfpWhite.copy(alpha = .35f), MaterialTheme.shapes.medium)
                                .padding(FfpSpace.large),
                            color = FfpWhite,
                            style = MaterialTheme.typography.labelMedium,
                        )
                    }
                }
                Box(Modifier.weight(0.46f).fillMaxSize()) {
                    PaleGeometry(Modifier.matchParentSize())
                    Column(
                        Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(FfpSpace.xLarge),
                        verticalArrangement = Arrangement.Center,
                        horizontalAlignment = Alignment.CenterHorizontally,
                    ) {
                        FfpLoginForm(profile, configured, auth, onSignIn,
                            openSettings, openPreview, onForgotPassword, modeToggle, mock, enrollTablet)
                    }
                }
            }
        } else {
            Column(
                Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(FfpSpace.xLarge),
                verticalArrangement = Arrangement.spacedBy(FfpSpace.large),
                horizontalAlignment = Alignment.CenterHorizontally,
            ) {
                FfpLoginForm(profile, configured, auth, onSignIn,
                    openSettings, openPreview, onForgotPassword, modeToggle, mock, enrollTablet, compact = true)
                FfpOfflineBanner(online)
                FfpSyncStatus("Unavailable")
            }
        }
    }
}

@Composable
internal fun FfpBrandHeader(profile: MobileProfile) {
    Column(verticalArrangement = Arrangement.spacedBy(FfpSpace.medium)) {
        Text(
            "Food For The Poor Jamaica · Approved logo pending",
            Modifier.border(1.dp, FfpWhite.copy(alpha = .55f), MaterialTheme.shapes.small).padding(FfpSpace.small),
            color = FfpWhite,
            style = MaterialTheme.typography.labelMedium,
        )
        Text("FFP Mobile", Modifier.semantics { heading() }, color = FfpWhite,
            style = MaterialTheme.typography.displaySmall)
        Box(Modifier.width(FfpSpace.action).height(3.dp).background(FfpGold))
        Text(profile.subtitle, color = FfpWhite, style = MaterialTheme.typography.titleLarge)
        Text(profile.description, color = FfpWhite, style = MaterialTheme.typography.bodyLarge)
    }
}

@Composable
internal fun FfpFeatureList(features: List<Section>) {
    Column(verticalArrangement = Arrangement.spacedBy(FfpSpace.large)) {
        features.forEach { FfpFeatureItem(it) }
    }
}

@Composable
internal fun FfpFeatureItem(feature: Section) {
    Row(horizontalArrangement = Arrangement.spacedBy(FfpSpace.medium)) {
        Box(Modifier.padding(top = FfpSpace.small).width(FfpSpace.small).height(3.dp).background(FfpGold))
        Column(verticalArrangement = Arrangement.spacedBy(FfpSpace.xSmall)) {
            Text(feature.title, color = FfpWhite, style = MaterialTheme.typography.titleMedium)
            if (feature.description.isNotBlank()) {
                Text(feature.description, color = FfpWhite, style = MaterialTheme.typography.bodyMedium)
            }
        }
    }
}

@Composable
internal fun FfpLoginForm(
    profile: MobileProfile,
    configured: Boolean,
    auth: LoginState,
    onSignIn: (String, String) -> Unit,
    openSettings: () -> Unit,
    openPreview: () -> Unit,
    onForgotPassword: () -> Unit,
    modeToggle: (() -> Unit)?,
    mock: Boolean,
    enrollTablet: (() -> Unit)?,
    compact: Boolean = false,
) {
    var username by remember { mutableStateOf("") }
    var password by remember { mutableStateOf("") }
    var attempted by remember { mutableStateOf(false) }
    var showServerError by remember { mutableStateOf(true) }
    val usernameError = if (attempted && username.isBlank()) "Enter your username." else null
    val passwordError = if (attempted && password.isBlank()) "Enter your password." else null
    val enabled = configured && !auth.loading
    fun submit() {
        if (!enabled) return
        attempted = true
        if (username.isNotBlank() && password.isNotBlank()) {
            showServerError = true
            onSignIn(username.trim(), password)
            password = ""
            attempted = false
        }
    }
    LaunchedEffect(configured, mock) {
        password = ""
    }
    Card(
        modifier = Modifier.widthIn(max = 520.dp).fillMaxWidth(),
        shape = MaterialTheme.shapes.large,
        colors = CardDefaults.cardColors(containerColor = FfpWhite),
        elevation = CardDefaults.cardElevation(defaultElevation = FfpSpace.cardElevation),
    ) {
        Column(Modifier.padding(FfpSpace.xLarge), verticalArrangement = Arrangement.spacedBy(FfpSpace.medium)) {
            if (compact) {
                Text("FFP Mobile", color = FfpPrimary, style = MaterialTheme.typography.titleLarge)
                Text("Food For The Poor Jamaica · Approved logo pending", color = FfpMuted,
                    style = MaterialTheme.typography.labelMedium)
            }
            Text("Welcome", Modifier.semantics { heading() }, style = MaterialTheme.typography.headlineMedium)
            Box(Modifier.width(FfpSpace.action).height(3.dp).background(FfpGold))
            Text(profile.subtitle, style = MaterialTheme.typography.titleMedium, color = FfpMuted)
            if (mock) Text("Mock sign-in · ${profile.application.demoLogin} / demo")
            else Text("Active Directory · FFTPJ.LOCAL")
            if (modeToggle != null) TextButton(modeToggle, enabled = !auth.loading) {
                Text(if (mock) "Use Active Directory" else "Use Mock sign-in")
            }
            FfpTextField(username, { username = it.take(256); showServerError = false }, "Username",
                enabled = enabled, error = usernameError,
                keyboard = KeyboardOptions(imeAction = ImeAction.Next))
            FfpPasswordField(password, { password = it.take(1024); showServerError = false }, enabled,
                passwordError, ::submit)
            TextButton(onForgotPassword, Modifier.align(Alignment.End).heightIn(min = FfpSpace.touch)) {
                Text("Forgot Password?")
            }
            FfpPrimaryButton(if (auth.loading) "Signing in…" else "Sign In", ::submit, Modifier.fillMaxWidth(),
                enabled = enabled, loading = auth.loading)
            when {
                !configured -> FfpStatusIndicator("Configure the private API in Device Settings.")
                showServerError -> auth.error?.let { FfpErrorDisplay(it) }
            }
            if (enrollTablet != null) {
                FfpSecondaryButton("Register or Check Tablet", enrollTablet, Modifier.fillMaxWidth(), enabled = enabled)
                auth.deviceStatus?.let { status -> SelectionContainer { Text(status) } }
            }
            FfpSecondaryButton("Device Settings", openSettings, Modifier.fillMaxWidth(), enabled = !auth.loading)
            TextButton(openPreview, Modifier.align(Alignment.CenterHorizontally).heightIn(min = FfpSpace.touch),
                enabled = !auth.loading) { Text("Open UI Preview") }
        }
    }
}

@Composable
internal fun FfpPasswordField(
    value: String,
    onChange: (String) -> Unit,
    enabled: Boolean,
    error: String?,
    onSubmit: () -> Unit,
) {
    var visible by remember { mutableStateOf(false) }
    LaunchedEffect(enabled) { if (!enabled) visible = false }
    FfpTextField(
        value, onChange, "Password", enabled = enabled, error = error,
        keyboard = KeyboardOptions(keyboardType = KeyboardType.Password, imeAction = ImeAction.Done),
        keyboardActions = KeyboardActions(onDone = { onSubmit() }),
        transformation = if (visible) VisualTransformation.None else PasswordVisualTransformation(),
        trailing = {
            TextButton({ visible = !visible }, Modifier.heightIn(min = FfpSpace.touch), enabled = enabled) {
                Text(if (visible) "Hide password" else "Show password")
            }
        },
    )
}

@Composable
private fun BrandGeometry(modifier: Modifier) {
    Canvas(modifier) {
        val curve = Path().apply {
            moveTo(0f, size.height * .8f)
            cubicTo(size.width * .35f, size.height * .58f, size.width * .64f, size.height * .95f,
                size.width, size.height * .65f)
        }
        drawPath(curve, FfpGold.copy(alpha = .7f), style = androidx.compose.ui.graphics.drawscope.Stroke(2.dp.toPx()))
        curve.lineTo(size.width, size.height)
        curve.lineTo(0f, size.height)
        curve.close()
        drawPath(curve, FfpPrimary.copy(alpha = .36f))
    }
}

@Composable
private fun PaleGeometry(modifier: Modifier) {
    Canvas(modifier) {
        drawCircle(FfpSoftBlue, radius = size.width * .53f, center = Offset(size.width, 0f))
        drawCircle(FfpSoftBlue.copy(alpha = .63f), radius = size.width * .31f,
            center = Offset(0f, size.height))
    }
}
