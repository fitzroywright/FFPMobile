package org.ffp.mobile.warehouse

import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.viewModels
import android.os.Bundle
import dagger.hilt.android.AndroidEntryPoint
import org.ffp.mobile.ui.MobileApp
import org.ffp.mobile.ui.MobileViewModel
import org.ffp.mobile.feature.warehouse.WarehouseProfile

@AndroidEntryPoint
class MainActivity : ComponentActivity() {
    private val model: MobileViewModel by viewModels()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent { MobileApp(WarehouseProfile, model) }
    }
}
