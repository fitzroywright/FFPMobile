package org.ffp.mobile.feature.warehouse

import org.ffp.mobile.network.MobileApplication
import org.ffp.mobile.ui.MobileProfile
import org.ffp.mobile.ui.Section

private val warehouseOperations = listOf(
    Section("Stock Counts", "Select a count sheet to review or count.", "stock-counts"),
    Section("Inventory Reports", "Review warehouse inventory.", "inventory-reports"),
    Section("Delivery Orders", "Review delivery paperwork.", "delivery-orders"),
    Section("Return to Stock", "Review returns to inventory.", "return-to-stock"),
    Section("Goods Receival", "Review goods awaiting receipt.", "goods-receival"),
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
