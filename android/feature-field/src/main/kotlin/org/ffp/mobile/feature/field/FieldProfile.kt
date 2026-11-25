package org.ffp.mobile.feature.field

import org.ffp.mobile.network.MobileApplication
import org.ffp.mobile.ui.MobileProfile
import org.ffp.mobile.ui.Section

val FieldProfile = MobileProfile(
    application = MobileApplication.FIELD,
    subtitle = "Field Officers",
    description = "Create and manage purchase orders and payment requests from the field.",
    capabilities = listOf(
        Section("Purchase Orders", "Create and manage purchase orders."),
        Section("Payment Requests", "Prepare and submit payment requests with supporting documentation."),
    ),
    dashboard = "Purchase orders and payment requests",
    sections = listOf(
        Section("My Purchase Orders", "Review purchase orders assigned to your account."),
        Section("My Payment Requests", "Review payment requests assigned to your account."),
        Section("Draft Documents", "Review documents before submission."),
        Section("Pending Approval", "Review documents awaiting approval."),
        Section("Recently Submitted", "Review recently submitted documents."),
        Section("Synchronization Status", "Review submission and acknowledgement status."),
    ),
    primaryActions = listOf(
        Section("New Purchase Order", "Create a purchase order under the purchasing rules set by FFP Manager."),
        Section("New Payment Request", "Prepare a payment request and supporting documentation."),
    ),
)
