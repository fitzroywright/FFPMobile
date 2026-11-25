package org.ffp.mobile.fieldofficers

import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.viewModels
import android.os.Bundle
import dagger.hilt.android.AndroidEntryPoint
import org.ffp.mobile.ui.MobileApp
import org.ffp.mobile.ui.MobileViewModel
import org.ffp.mobile.feature.field.FieldProfile

@AndroidEntryPoint
class MainActivity : ComponentActivity() {
    private val model: MobileViewModel by viewModels()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent { MobileApp(FieldProfile, model) }
    }
}
