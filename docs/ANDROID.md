# Android development

## Open and run in Android Studio

1. In Android Studio, select **File > Open**, choose the repository's `android/` directory, and open it as a Gradle project.
2. If Android Studio reports a Java runtime error during sync, open **Android Studio > Settings > Build, Execution, Deployment > Build Tools > Gradle**. Under **Gradle JDK**, select the bundled **JetBrains Runtime** or **GRADLE_LOCAL_JAVA_HOME**. If Android Studio shows **Gradle Daemon JVM criteria**, use Java **25**. If you need to add the bundled runtime manually, use `/Applications/Android Studio.app/Contents/jbr/Contents/Home`. The Gradle wrapper pins Gradle 9.6.0. The project uses Android Gradle Plugin 9.5.2, Kotlin 2.2.10, Compose BOM 2025.10.01 and Java 17 bytecode.
3. Open **Tools > SDK Manager**. Install Android SDK Platform 37, Android SDK Build-Tools 36.0.0, Android SDK Platform-Tools, and Android Emulator. Select **File > Sync Project with Gradle Files**.
4. Open **Tools > Device Manager** and start **FFP-Tablet** if it exists. To create one, select **Create device > Tablet > Pixel Tablet** and install an API 36 system image. Choose x86_64 on an Intel Mac or arm64 on Apple silicon. Rotate the emulator to landscape.
5. In the run configuration selector, choose `app-field-officers`, `app-warehouse` or `app-security-post`, select the running emulator, and click **Run**. If needed, add an **Android App** run configuration and select the app's module.
6. Tap **Open UI Preview** to browse that app's dashboard and screens. Use **Exit Preview** to return to sign-in.

Compile and target SDK are 37 and the minimum OS is Android 8.0 (API 26). The apps can install side by side.

## Build and test

From the repository root:

```sh
./scripts/test-android.sh
./scripts/test-android.sh --install
```

The first command builds three debug APKs and runs JVM unit tests. The second also installs and starts the apps on a running emulator. Set `ANDROID_SERIAL` when more than one emulator is connected. Use `JAVA_HOME` and `ANDROID_HOME` for custom installations.

| Module | Debug APK |
| --- | --- |
| `app-field-officers` | `android/app-field-officers/build/outputs/apk/debug/app-field-officers-debug.apk` |
| `app-warehouse` | `android/app-warehouse/build/outputs/apk/debug/app-warehouse-debug.apk` |
| `app-security-post` | `android/app-security-post/build/outputs/apk/debug/app-security-post-debug.apk` |

In Android Studio, set the desired module to `debug` in **Build Variants** and use **Build > Generate App Bundles or APKs > Generate APKs**.

## Private API endpoint

From sign-in, open **Device Settings**, enter the private HTTPS base URL from your administrator, and tap **Save**. Leave the URL blank to clear it. Use a VPN or the approved internal network and a certificate trusted by Android. The endpoint is configured separately per app. Sign-in and synchronization controls remain disabled in the Kotlin apps.

The UI preview has no account session, transaction access or live data. The Warehouse API requires a commissioned permission evaluator and approved account bindings. See [Warehouse Phase 1](WAREHOUSE-PHASE-1.md) for the server integration. See the [release gates](IMPLEMENTATION.md) for remaining integration and operational work.

## Verification for this conversion

The Intel Mac build used Android Studio 2026.2 and its JDK 25.0.4. All three debug APKs built, six JVM tests passed, and all three apps installed and cold-launched on the API 36 FFP-Tablet emulator. Preview navigation and logcat checks remain unverified. The encrypted outbox device test hit a compile error in the test's database-close code. The test source was corrected and still needs to run on a device.

To run the encrypted outbox test with an emulator connected, open a terminal in `android/` and run `./gradlew :core-database:connectedDebugAndroidTest`.
