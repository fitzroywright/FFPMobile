package org.ffp.mobile.feature.security

import org.ffp.mobile.network.MobileApplication
import org.ffp.mobile.ui.MobileProfile
import org.ffp.mobile.ui.Section

val SecurityProfile = MobileProfile(
    application = MobileApplication.SECURITY,
    subtitle = "Security Post",
    description = "Review dispatch checks and checkpoint activity.",
    capabilities = listOf(
        Section("Dispatch Verification", "Check release authorization before goods leave the site."),
        Section("Vehicle Movements", "Review vehicle arrivals and departures."),
        Section("Visitor Log", "Review checkpoint visitor activity."),
    ),
    dashboard = "Browse dispatch checks and checkpoint activity.",
    sections = listOf(
        Section("Dispatch Verification", "Physical release requires current authorization from the server."),
        Section("Vehicle Movements", "Review vehicle arrivals and departures."),
        Section("Visitor Log", "Review checkpoint visitor activity."),
        Section("Pending Events", "Review checkpoint events awaiting server confirmation."),
        Section("Synchronization", "View acknowledgement and conflict status for checkpoint events."),
    ),
)
