package org.ffp.mobile.feature.warehouse

import org.ffp.mobile.network.MobileApplication
import org.ffp.mobile.ui.MobileProfile
import org.ffp.mobile.ui.Section

private val warehouseOperations = listOf(
    Section("Stock Count", "Select a count sheet to review or count."),
    Section("Inventory Report", "Review warehouse inventory."),
    Section("Delivery Orders", "Review delivery paperwork."),
    Section("Return to Stock", "Review returns to inventory."),
    Section("Goods Receival", "Review goods awaiting receipt."),
)

val WarehouseProfile = MobileProfile(
    application = MobileApplication.WAREHOUSE,
    subtitle = "Warehouse & Operations",
    description = "Manage warehouse inventory, goods movement and operational documents.",
    capabilities = warehouseOperations.map { Section(it.title, "") },
    dashboard = "Warehouse operations",
    sections = warehouseOperations + listOf(
        Section("My Drafts", "Review documents before submission."),
        Section("Synchronization Status", "Review locally saved, received, accepted, approved and posted transactions."),
    ),
    dashboardStatus = listOf("Pending Counts", "Open Delivery Orders", "Goods Awaiting Receipt", "Pending Returns"),
)
