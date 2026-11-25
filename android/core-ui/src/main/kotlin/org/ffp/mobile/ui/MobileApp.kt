package org.ffp.mobile.ui

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.safeDrawingPadding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle

@Composable
fun MobileApp(profile: MobileProfile, model: MobileViewModel) {
    var preview by rememberSaveable { mutableStateOf(false) }
    var section by rememberSaveable { mutableIntStateOf(-1) }
    var settingsOpen by rememberSaveable { mutableStateOf(false) }
    var passwordHelpOpen by rememberSaveable { mutableStateOf(false) }
    val online by model.online.collectAsStateWithLifecycle()
    val endpoint by model.endpoint.collectAsStateWithLifecycle()
    val login by model.login.collectAsStateWithLifecycle()
    val account = login.account
    val canMock = model.mockAvailable
    var mock by rememberSaveable { mutableStateOf(canMock) }

    fun back() {
        if (section >= 0) section = -1 else preview = false
    }

    BackHandler(preview) { back() }
    FfpAppTheme {
        Surface(Modifier.fillMaxSize(), color = MaterialTheme.colorScheme.background) {
            Box(Modifier.fillMaxSize().safeDrawingPadding()) {
                when {
                    account != null -> Column(
                        Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(FfpSpace.xLarge),
                        verticalArrangement = Arrangement.spacedBy(FfpSpace.large),
                    ) {
                        FfpTopAppBar(profile.subtitle, "Sign Out", model::signOut)
                        Text("Welcome, ${account.displayName}", style = MaterialTheme.typography.headlineMedium)
                        if (account.mock) FfpStatusIndicator("MOCK SESSION", FfpPrimary)
                        DestinationGrid(profile.sections.filter { it.permissionId in account.authorizedModules }) {}
                        FfpOfflineBanner(online)
                        FfpSyncStatus("Unavailable")
                    }
                    preview -> PreviewScreen(profile, section, { section = it }, ::back)
                    else -> FfpLoginScaffold(
                        profile = profile,
                        online = online,
                        configured = mock || endpoint.isNotBlank(),
                        auth = login,
                        onSignIn = { username, password -> model.signIn(profile.application, username, password, mock) },
                        openSettings = { settingsOpen = true },
                        openPreview = { preview = true },
                        onForgotPassword = { passwordHelpOpen = true },
                        modeToggle = if (canMock) ({ mock = !mock }) else null,
                        mock = mock && canMock,
                        enrollTablet = if (!mock) ({ model.enrollTablet(profile.application) }) else null,
                    )
                }
            }
            if (settingsOpen) {
                EndpointDialog(endpoint, { settingsOpen = false }, model::saveEndpoint)
            }
            if (passwordHelpOpen) {
                FfpConfirmationDialog(
                    title = "Password help",
                    message = "Contact your FFP Manager administrator for help with your account.",
                    confirmLabel = "Close",
                    onConfirm = { passwordHelpOpen = false },
                    onDismiss = { passwordHelpOpen = false },
                    dismissLabel = null,
                )
            }
        }
    }
}

@Composable
private fun EndpointDialog(endpoint: String, close: () -> Unit, save: (String, (Boolean) -> Unit) -> Unit) {
    var input by rememberSaveable(endpoint) { mutableStateOf(endpoint) }
    var invalid by rememberSaveable { mutableStateOf(false) }
    AlertDialog(
        onDismissRequest = close,
        title = { Text("Device Settings") },
        text = {
            FfpTextField(
                input, { input = it; invalid = false }, "Private HTTPS API URL",
                error = if (invalid) "Enter an HTTPS URL without credentials, a query or a fragment." else null,
                hint = "Leave empty to clear the configured address.",
                keyboard = KeyboardOptions(keyboardType = KeyboardType.Uri),
            )
        },
        confirmButton = { TextButton({ save(input) { if (it) close() else invalid = true } }) { Text("Save") } },
        dismissButton = { TextButton(close) { Text("Cancel") } },
    )
}

@Composable
private fun PreviewScreen(profile: MobileProfile, selected: Int, select: (Int) -> Unit, back: () -> Unit) {
    val destinations = profile.sections + profile.primaryActions
    val section = destinations.getOrNull(selected)
    BoxWithConstraints(Modifier.fillMaxSize()) {
        val wide = maxWidth >= 1000.dp && maxWidth > maxHeight
        Row(Modifier.fillMaxSize()) {
            if (wide) {
                FfpNavigationRail(listOf("Dashboard") + profile.sections.map { it.title }, selected + 1) { index ->
                    select(index - 1)
                }
            }
            Column(
                Modifier.weight(1f).verticalScroll(rememberScrollState()).padding(FfpSpace.xLarge),
                verticalArrangement = Arrangement.spacedBy(FfpSpace.large),
            ) {
                FfpTopAppBar(profile.subtitle, if (section == null) "Exit Preview" else "Back to Dashboard", back)
                FfpStatusIndicator("UI PREVIEW", FfpPrimary)
                Text(section?.title ?: "Dashboard", Modifier.semantics { heading() },
                    style = MaterialTheme.typography.headlineMedium)
                Text("No authenticated session. Production data and actions are unavailable.")
                if (section == null) {
                    Text(profile.dashboard)
                    profile.primaryActions.forEachIndexed { index, action ->
                        FfpPrimaryButton(action.title, { select(profile.sections.size + index) })
                    }
                    if (profile.dashboardStatus.isNotEmpty()) {
                        FfpDocumentCard("Operational status") {
                            profile.dashboardStatus.forEach { FfpStatusIndicator("$it · Unavailable") }
                        }
                    }
                    DestinationGrid(profile.sections, select)
                } else {
                    FfpDocumentCard(section.title) {
                        Text(section.description)
                        if (section.title.startsWith("Synchronization")) {
                            FfpSyncStatus("Unavailable")
                        } else {
                            FfpDataGrid(emptyList(), emptyList(), "No records are loaded in preview.")
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun DestinationGrid(sections: List<Section>, select: (Int) -> Unit) {
    BoxWithConstraints {
        val columns = if (maxWidth >= 800.dp) 3 else if (maxWidth >= 520.dp) 2 else 1
        Column(verticalArrangement = Arrangement.spacedBy(FfpSpace.medium)) {
            sections.indices.chunked(columns).forEach { indices ->
                Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(FfpSpace.medium)) {
                    indices.forEach { index ->
                        FfpDashboardCard(sections[index], Modifier.weight(1f)) { select(index) }
                    }
                    repeat(columns - indices.size) { Box(Modifier.weight(1f)) }
                }
            }
        }
    }
}
