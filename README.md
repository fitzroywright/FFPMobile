# FFP Mobile

Three native Android tablet apps share a Kotlin and Jetpack Compose foundation. Each installs separately and opens to a branded sign-in screen and an isolated UI preview.

| App | Android package |
| --- | --- |
| Field Officers | `org.ffp.mobile.fieldofficers` |
| Warehouse & Operations | `org.ffp.mobile.warehouse` |
| Security Post | `org.ffp.mobile.security` |

Open `android/` in Android Studio. See the [Android development guide](docs/ANDROID.md) for setup, APKs, the emulator and device settings.

The .NET API under `services/FFPMobile.Api` contains the Warehouse authentication and tablet enrollment endpoints. The Kotlin apps currently offer UI previews. Live sign-in, enrollment and transaction synchronization still need to be integrated into the Kotlin apps. See the [architecture](docs/ARCHITECTURE.md) and [release gates](docs/IMPLEMENTATION.md).
