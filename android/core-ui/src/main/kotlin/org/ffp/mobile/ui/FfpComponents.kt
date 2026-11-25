package org.ffp.mobile.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.NavigationRail
import androidx.compose.material3.NavigationRailItem
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.semantics.LiveRegionMode
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.liveRegion
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.input.VisualTransformation
import androidx.compose.ui.unit.dp

@Composable
internal fun FfpTextField(
    value: String,
    onChange: (String) -> Unit,
    label: String,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
    error: String? = null,
    hint: String? = null,
    keyboard: KeyboardOptions = KeyboardOptions.Default,
    keyboardActions: KeyboardActions = KeyboardActions.Default,
    transformation: VisualTransformation = VisualTransformation.None,
    trailing: (@Composable () -> Unit)? = null,
) {
    val supportingText = error ?: hint
    OutlinedTextField(
        value = value,
        onValueChange = onChange,
        modifier = modifier.fillMaxWidth(),
        label = { Text(label) },
        enabled = enabled,
        isError = error != null,
        supportingText = supportingText?.let { { Text(it) } },
        singleLine = true,
        keyboardOptions = keyboard,
        keyboardActions = keyboardActions,
        visualTransformation = transformation,
        trailingIcon = trailing,
        shape = MaterialTheme.shapes.small,
    )
}

@Composable
internal fun FfpPrimaryButton(
    label: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
    loading: Boolean = false,
) {
    Button(onClick, modifier.heightIn(min = FfpSpace.action), enabled = enabled && !loading,
        shape = MaterialTheme.shapes.small) {
        if (loading) FfpLoadingIndicator(label) else Text(label)
    }
}

@Composable
internal fun FfpSecondaryButton(
    label: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
) {
    OutlinedButton(onClick, modifier.heightIn(min = FfpSpace.touch), enabled = enabled,
        shape = MaterialTheme.shapes.small) { Text(label) }
}

@Composable
internal fun FfpLoadingIndicator(label: String) {
    Row(
        Modifier.semantics { liveRegion = LiveRegionMode.Polite },
        horizontalArrangement = Arrangement.spacedBy(FfpSpace.small),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        CircularProgressIndicator(Modifier.size(18.dp), strokeWidth = 2.dp)
        Text(label)
    }
}

@Composable
internal fun FfpErrorDisplay(message: String) {
    Text(
        message,
        Modifier.semantics { liveRegion = LiveRegionMode.Polite },
        color = MaterialTheme.colorScheme.error,
        style = MaterialTheme.typography.bodyMedium,
    )
}

@Composable
internal fun FfpStatusIndicator(label: String, color: Color = MaterialTheme.colorScheme.onSurface) {
    Text(label, color = color, style = MaterialTheme.typography.labelLarge)
}

@Composable
internal fun FfpOfflineBanner(online: Boolean, onBrandBackground: Boolean = false) {
    val color = if (onBrandBackground) FfpWhite else if (online) FfpSuccess else FfpMuted
    FfpStatusIndicator(if (online) "Network available" else "Network unavailable", color)
}

@Composable
internal fun FfpSyncStatus(status: String, color: Color = MaterialTheme.colorScheme.onSurface) {
    FfpStatusIndicator("Synchronization: $status", color)
}

@Composable
internal fun FfpTopAppBar(title: String, actionLabel: String, action: () -> Unit) {
    Row(verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(FfpSpace.medium)) {
        Text(title, Modifier.weight(1f).semantics { heading() }, style = MaterialTheme.typography.titleLarge)
        TextButton(action, Modifier.heightIn(min = FfpSpace.touch)) { Text(actionLabel) }
    }
}

@Composable
internal fun FfpNavigationRail(
    destinations: List<String>,
    selected: Int,
    onSelect: (Int) -> Unit,
) {
    NavigationRail(containerColor = MaterialTheme.colorScheme.surface) {
        Column(Modifier.verticalScroll(rememberScrollState())) {
            destinations.forEachIndexed { index, name ->
                NavigationRailItem(
                    selected = index == selected,
                    onClick = { onSelect(index) },
                    icon = { Text(name.take(1), Modifier.clearAndSetSemantics { }) },
                    label = { Text(name) },
                )
            }
        }
    }
}

@Composable
internal fun FfpDashboardCard(section: Section, modifier: Modifier = Modifier, onClick: () -> Unit) {
    Card(onClick = onClick, modifier = modifier.heightIn(min = 88.dp)) {
        Column(Modifier.padding(FfpSpace.large), verticalArrangement = Arrangement.spacedBy(FfpSpace.xSmall)) {
            Text(section.title, style = MaterialTheme.typography.titleMedium)
            Text(section.description, style = MaterialTheme.typography.bodyMedium)
        }
    }
}

@Composable
internal fun FfpDocumentCard(title: String, body: @Composable () -> Unit) {
    Surface(Modifier.fillMaxWidth(), shape = MaterialTheme.shapes.medium, color = MaterialTheme.colorScheme.surface) {
        Column(Modifier.padding(FfpSpace.large), verticalArrangement = Arrangement.spacedBy(FfpSpace.small)) {
            Text(title, Modifier.semantics { heading() }, style = MaterialTheme.typography.titleMedium)
            body()
        }
    }
}

@Composable
internal fun FfpDataGrid(headings: List<String>, rows: List<List<String>>, emptyMessage: String) {
    if (rows.isEmpty()) {
        Text(emptyMessage, style = MaterialTheme.typography.bodyMedium)
        return
    }
    Column(verticalArrangement = Arrangement.spacedBy(FfpSpace.small)) {
        Row(horizontalArrangement = Arrangement.spacedBy(FfpSpace.small)) {
            headings.forEach { Text(it, Modifier.weight(1f), style = MaterialTheme.typography.titleMedium) }
        }
        rows.forEach { cells ->
            Row(horizontalArrangement = Arrangement.spacedBy(FfpSpace.small)) {
                headings.forEachIndexed { index, _ ->
                    Text(cells.getOrElse(index) { "" }, Modifier.weight(1f))
                }
            }
        }
    }
}

@Composable
internal fun FfpConfirmationDialog(
    title: String,
    message: String,
    confirmLabel: String,
    onConfirm: () -> Unit,
    onDismiss: () -> Unit,
    dismissLabel: String? = "Cancel",
) {
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(title) },
        text = { Text(message) },
        confirmButton = { TextButton(onConfirm) { Text(confirmLabel) } },
        dismissButton = dismissLabel?.let { { TextButton(onDismiss) { Text(it) } } },
    )
}
