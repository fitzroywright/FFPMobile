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

For Warehouse AD sign-in, select **Use Active Directory** on a debug build, then confirm the approved API URL in **Device Settings**. Select **Register or Check Tablet** to enroll the Keystore-backed installation and obtain the installation ID without entering credentials. An authorized device administrator must approve the installation. After approval, FFPMobile.Api authenticates the employee through the Common.Security Active Directory provider for `FFTPJ.LOCAL` and obtains live permissions from the FFP Manager evaluator.

The Windows emulator debug build defaults to the loopback HTTPS helper at `https://localhost:7241/` through ADB reverse, with the same local certificate setup documented below. Release uses the configured private HTTPS endpoint and standard Android certificate trust.

The API's default directory settings use `DC=FFTPJ,DC=LOCAL` with LDAPS on port 636 to `jmad01.fftpj.local` and `jmad02.fftpj.local`. Windows verified TLS against both named directory controllers. The approved mobile origin still needs internal DNS, a trusted certificate and a commissioned XAF evaluator for real employee access.

If a device administrator also uses a mobile app, an approver can add that existing Common.Security account ID to `Security:Warehouse:AdditionalAdministratorAccountIds` for Warehouse or `Security:Applications:<application-id>:AdditionalAdministratorAccountIds` for Field Officers or Security Post. The account keeps its original AD binding and the app's evaluator must still authorize its modules.

Field Officers and Security Post use the same private API, Common.Security AD provider and tablet approval controls. Select **Use Active Directory**, then **Register or Check Tablet** in each app. Each installation belongs to one application and requires its own approval. Configure each authoritative permission evaluator independently under `FfpManager:Applications:field-officers` or `FfpManager:Applications:security-post`. Each section takes an HTTPS `PermissionServiceOrigin`, an OpenBao `ServiceTokenSecret` and verified `Bindings` using the same format as Warehouse. The evaluator exposes `POST /v1/field-officers/permissions` or `POST /v1/security-post/permissions` and returns the same decision shape as the Warehouse evaluator. Missing bindings or decisions prevent live access.

## Windows Mock sign-in

All three debug apps can call the isolated Common.Security Mock helper in FFPMobile.Api. Only the emulator's local HTTPS endpoint is used, with the Windows development certificate explicitly trusted by that debug client. Demo responses carry app-specific dashboard tiles and never contain an API access token.

From the repository root, with an API 37 emulator running (use its serial from `adb devices`), run:

```powershell
$env:JAVA_HOME = 'C:\Program Files\Android\Android Studio\jbr'
$env:ANDROID_HOME = Join-Path $env:LOCALAPPDATA 'Android\Sdk'
$adb = Join-Path $env:ANDROID_HOME 'platform-tools\adb.exe'
dotnet dev-certs https --trust
$certDir = Join-Path $PWD 'android\core-auth\build\mock-tls-assets'
New-Item -ItemType Directory -Force $certDir | Out-Null
dotnet dev-certs https --export-path (Join-Path $certDir 'warehouse-mock.cer') --format PEM
& $adb -s emulator-5556 reverse tcp:7241 tcp:7241
Push-Location android
.\gradlew.bat :app-warehouse:assembleDebug :app-field-officers:assembleDebug :app-security-post:assembleDebug
& $adb -s emulator-5556 install -r app-warehouse\build\outputs\apk\debug\app-warehouse-debug.apk
& $adb -s emulator-5556 install -r app-field-officers\build\outputs\apk\debug\app-field-officers-debug.apk
& $adb -s emulator-5556 install -r app-security-post\build\outputs\apk\debug\app-security-post-debug.apk
Pop-Location
$data = Join-Path $PWD 'artifacts\mock-data'
New-Item -ItemType Directory -Force $data | Out-Null
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Security__SqlitePath = Join-Path $data 'accounts.db'
$env:Devices__SqlitePath = Join-Path $data 'devices.db'
$env:Security__Mock__Enabled = 'true'
$env:Security__Mock__IsolationConfirmed = 'true'
$env:Security__Mock__Password = 'demo'
$env:Security__Mock__Identities__0__LoginName = 'warehouse-demo'
$env:Security__Mock__Identities__0__SubjectId = 'warehouse-demo'
$env:Security__Mock__Identities__0__DisplayName = 'Warehouse Demo'
$env:Security__Mock__Identities__1__LoginName = 'field-demo'
$env:Security__Mock__Identities__1__SubjectId = 'field-demo'
$env:Security__Mock__Identities__1__DisplayName = 'Field Officers Demo'
$env:Security__Mock__Identities__2__LoginName = 'security-demo'
$env:Security__Mock__Identities__2__SubjectId = 'security-demo'
$env:Security__Mock__Identities__2__DisplayName = 'Security Post Demo'
dotnet run --project services\FFPMobile.Api --no-launch-profile -p:CommonSecurityRoot=D:/Projects/Common.Security -p:CommonRoot=D:/Projects/Common -- --urls https://localhost:7241
```

Use password `demo` with `warehouse-demo`, `field-demo` or `security-demo` in the corresponding app. OIDC requires an approved issuer, mobile redirect registration, and verified organizational account and FFP Manager bindings.

The UI preview has no account session, transaction access or live data. The Warehouse API requires a commissioned permission evaluator and approved account bindings. See [Warehouse Phase 1](WAREHOUSE-PHASE-1.md) for the server integration. See the [release gates](IMPLEMENTATION.md) for remaining integration and operational work.

## Verification for this conversion

The Intel Mac build used Android Studio 2026.2 and its JDK 25.0.4. All three debug APKs built, six JVM tests passed, and all three apps installed and cold-launched on the API 36 FFP-Tablet emulator. Preview navigation and logcat checks remain unverified. The encrypted outbox device test hit a compile error in the test's database-close code. The test source was corrected and still needs to run on a device.

To run the encrypted outbox test with an emulator connected, open a terminal in `android/` and run `./gradlew :core-database:connectedDebugAndroidTest`.
