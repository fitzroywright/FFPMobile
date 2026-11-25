package org.ffp.mobile.ui

import android.content.Context
import android.net.ConnectivityManager
import android.net.Network
import android.net.NetworkCapabilities
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.channels.awaitClose
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.callbackFlow
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import org.ffp.mobile.auth.AccountSession
import org.ffp.mobile.auth.AuthRepository
import org.ffp.mobile.auth.AuthenticationFeedback
import org.ffp.mobile.auth.DeviceSettings
import org.ffp.mobile.network.MobileApplication
import org.json.JSONObject
import retrofit2.HttpException
import java.io.IOException
import java.time.Duration
import java.time.Instant
import kotlinx.coroutines.delay
import kotlinx.coroutines.ensureActive
import javax.inject.Inject

data class MobileProfile(
    val application: MobileApplication,
    val subtitle: String,
    val description: String,
    val capabilities: List<Section>,
    val dashboard: String,
    val sections: List<Section>,
    val primaryActions: List<Section> = emptyList(),
    val dashboardStatus: List<String> = emptyList(),
)

data class LoginState(
    val loading: Boolean = false,
    val error: String? = null,
    val account: AccountSession? = null,
    val deviceStatus: String? = null,
)
data class Section(val title: String, val description: String, val permissionId: String = "")

@HiltViewModel
class MobileViewModel @Inject constructor(
    @ApplicationContext context: Context,
    private val settings: DeviceSettings,
    private val authentication: AuthRepository,
) : ViewModel() {
    private val _endpoint = MutableStateFlow(settings.endpoint())
    val endpoint = _endpoint.asStateFlow()
    val mockAvailable = authentication.mockAvailable
    private val _login = MutableStateFlow(LoginState())
    val login = _login.asStateFlow()

    fun enrollTablet(application: MobileApplication) {
        if (_login.value.loading || _login.value.account != null) return
        _login.value = LoginState(loading = true)
        viewModelScope.launch {
            _login.value = try {
                LoginState(deviceStatus = authentication.enrollTablet(application))
            } catch (feedback: AuthenticationFeedback) {
                LoginState(error = feedback.message)
            } catch (_: IOException) {
                LoginState(error = "Could not reach the private API. Check Device Settings and your network.")
            } catch (_: IllegalArgumentException) {
                LoginState(error = "Enter the private HTTPS API origin in Device Settings.")
            } catch (_: Exception) {
                kotlinx.coroutines.currentCoroutineContext().ensureActive()
                LoginState(error = "The Mobile API could not complete tablet enrollment.")
            }
        }
    }

    fun signIn(application: MobileApplication, username: String, password: String, mock: Boolean) {
        if (_login.value.loading || _login.value.account != null) return
        _login.value = LoginState(loading = true)
        viewModelScope.launch {
            val account = try {
                authentication.signIn(application, username, password, mock)
            } catch (feedback: AuthenticationFeedback) {
                _login.value = LoginState(error = feedback.message)
                return@launch
            } catch (failure: HttpException) {
                val message = when (failure.code()) {
                    401 -> "Sign-in was denied. Check your credentials or ask your administrator to verify your account."
                    403 -> {
                        val code = failure.response()?.errorBody()?.use { body ->
                            runCatching { JSONObject(body.string()).optString("code") }.getOrNull()
                        }
                        when (code) {
                            "warehouse-access-denied" -> "Your account was verified. Warehouse permissions are unavailable or have not been granted."
                            "application-permissions-unavailable" -> "Your account was verified. Permissions for this application are not configured."
                            "application-mismatch" -> "This installation is registered to another application."
                            else -> "Access was denied. Check the tablet approval or contact your administrator."
                        }
                    }
                    429 -> "Too many sign-in attempts. Try again later."
                    else -> "The sign-in service is unavailable."
                }
                _login.value = LoginState(error = message)
                return@launch
            } catch (_: IOException) {
                _login.value = LoginState(error = "Could not reach the private API. Check your network connection.")
                return@launch
            } catch (_: IllegalArgumentException) {
                _login.value = LoginState(error = "Check your credentials and private API settings.")
                return@launch
            } catch (_: Exception) {
                kotlinx.coroutines.currentCoroutineContext().ensureActive()
                _login.value = LoginState(error = "The sign-in service did not complete the request.")
                return@launch
            }
            _login.value = LoginState(account = account)
            delay(Duration.between(Instant.now(), account.expiresAt).toMillis().coerceAtLeast(0))
            if (_login.value.account === account) signOut()
        }
    }

    fun signOut() {
        val account = _login.value.account ?: return
        _login.value = LoginState(loading = true)
        viewModelScope.launch {
            try {
                if (!account.mock) authentication.clearSession()
                _login.value = LoginState()
            } catch (_: IllegalStateException) {
                _login.value = LoginState(error = "Could not clear saved credentials. Contact your administrator.")
            }
        }
    }

    private val manager = context.getSystemService(ConnectivityManager::class.java)
    val online = callbackFlow {
        fun report() {
            val capabilities = manager.getNetworkCapabilities(manager.activeNetwork)
            trySend(capabilities?.hasCapability(NetworkCapabilities.NET_CAPABILITY_VALIDATED) == true)
        }
        val callback = object : ConnectivityManager.NetworkCallback() {
            override fun onCapabilitiesChanged(network: Network, capabilities: NetworkCapabilities) {
                trySend(capabilities.hasCapability(NetworkCapabilities.NET_CAPABILITY_VALIDATED))
            }
            override fun onLost(network: Network) {
                trySend(false)
            }
        }
        manager.registerDefaultNetworkCallback(callback)
        report()
        awaitClose { manager.unregisterNetworkCallback(callback) }
    }.stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), false)

    fun saveEndpoint(value: String, onResult: (Boolean) -> Unit) {
        viewModelScope.launch {
            val saved = withContext(Dispatchers.IO) { settings.saveEndpoint(value) }
            if (saved) _endpoint.value = settings.endpoint()
            onResult(saved)
        }
    }
}
