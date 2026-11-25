# Warehouse and Operations Phase 1

The Android app remains a .NET MAUI tablet app. The working tree, tracked branches and existing Android build files contained no Kotlin or Gradle implementation. The Warehouse app uses the existing FFP colors, branding, centered login card and dashboard layout. It renders five server-defined capability IDs as tiles and includes a tile only when a current API response grants it. There are no transaction screens.

## Windows API and Android commands

Clone the approved Common.Security 2.0 and Common source revisions beside FFPMobile. The server uses the existing Common.Security core from the standalone checkout and compiles the standalone Active Directory plugin's original files against that core. The standalone plugin project currently targets .NET 10 while its core targets .NET 11. The nested Common copy of Common.Security lacks the approved-account and opaque-session APIs. Override `CommonSecurityRoot` and `CommonRoot` in MSBuild if the checkouts are elsewhere. Use an installed .NET 11 SDK, Android SDK Platform 37, build-tools 37.0.0 and JDK 21.

Run the API from PowerShell. The default local configuration starts the API for liveness and tablet enrollment. Readiness and Warehouse access remain closed without an approved evaluator and bindings.

```powershell
Set-Location D:\Projects\FFPMobile
dotnet dev-certs https --trust
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Security__SqlitePath = "$PWD\artifacts\mobile-accounts.db"
$env:Devices__SqlitePath = "$PWD\artifacts\mobile-devices.db"
dotnet build .\services\FFPMobile.Api --artifacts-path .\artifacts -m:1 -p:UseSharedCompilation=false
Push-Location .\services\FFPMobile.Api
dotnet ..\..\artifacts\bin\FFPMobile.Api\debug\FFPMobile.Api.dll --urls https://localhost:7240
Pop-Location
```

For organizational sign-in, set `$env:FFPMOBILE_CONFIG_FILE` to the absolute path of a protected, commissioned JSON configuration before starting the API. The file controls exact LDAPS hosts and search base, XAF bindings, and the approved secret provider. Release tablet builds use `https://mobile.ffpja.org/`. Debug builds allow an HTTPS origin under **Device Settings**. Local emulator testing requires a reachable hostname and a certificate whose SAN and trusted chain match that hostname. The Windows localhost developer certificate alone is insufficient for Android emulator connections.

Build and launch only Warehouse from a separate Windows PowerShell window after starting an Android tablet emulator. If more than one device is online, replace `emulator-5554` with the intended serial shown by ADB.

```powershell
Set-Location D:\Projects\FFPMobile
$androidSdk = Join-Path $env:LOCALAPPDATA 'Android\Sdk'
$javaSdk = 'C:\Program Files\Android\openjdk\jdk-21.0.8'
$adb = Join-Path $androidSdk 'platform-tools\adb.exe'
dotnet build .\apps\FFPMobile.WarehouseOperations -c Debug "-p:AndroidSdkDirectory=$androidSdk" "-p:JavaSdkDirectory=$javaSdk"
& $adb devices
& $adb -s emulator-5554 install -r .\apps\FFPMobile.WarehouseOperations\bin\Debug\net11.0-android\org.ffp.mobile.warehouse-Signed.apk
$activity = (& $adb -s emulator-5554 shell cmd package resolve-activity --brief -a android.intent.action.MAIN -c android.intent.category.LAUNCHER -p org.ffp.mobile.warehouse | Select-Object -Last 1).Trim()
& $adb -s emulator-5554 shell am start -W -n $activity
dotnet test .\tests\FFPMobile.Api.Tests --artifacts-path .\artifacts -m:1 -p:UseSharedCompilation=false
```

## Authentication and device approval

- Common.Security validates the password over LDAPS and returns immutable AD domain and user object GUIDs. Only approved active Common.Security bindings can authenticate. Provision them with the existing offline Common.Security maintenance tool for the `ffp-mobile` application scope. Import an authorized manifest as active `Staff` or `Administrator` with the `active-directory` binding. Use the LDAP domain object GUID for `scopeId` and user object GUID for `subjectId`. Run the tool from its checkout using the following commands from Windows. Set the protected manifest path and the accountable approver before running. For Kratos, the SQLite file must remain writable by the service account.
- A tablet generates a P-256 signing key with Android Keystore. Its installation ID is the SHA-256 fingerprint of the public key. Each protected request signs the method, path, ID, fresh nonce, timestamp and token digest. The server consumes the nonce once and requires clock skew within 90 seconds. Keep tablets synchronized with network time. A registration starts Pending Approval and is durable across API restarts. Approvers match the full installation ID shown on the tablet to inventory before approval.
- The approval API requires fresh organizational credentials, an active Common.Security Administrator binding and an explicitly approved immutable account ID in `Devices:AdministratorAccountIds`. nginx also restricts administrative URLs to the management network. The API records registration and status changes in SQLite and emits authentication and session audit events to the service log. Revocation is terminal for an installation. Suspension and reapproval invalidate the tablet's existing sessions.
- Opaque tokens live for 30 minutes, rotate while still valid, and require sign-in again after at most eight hours. Signing in as a new employee invalidates the previous session on that tablet. The app stores the current token and expiry only in MAUI SecureStorage, clears them on sign-out, and checks the server before restoring the dashboard. It checks permission changes every 20 seconds while in the foreground. Active Directory status is verified at password authentication. Session requests recheck Common.Security account status/version and require current XAF permission decisions. To invalidate an active session immediately after an AD-only account change, revoke that Common.Security account's sessions or disable its binding.
- Offline interfaces define encrypted storage, reference data, a device-bound grant, permission revalidation, and durable operations with idempotency keys. The Warehouse app requires the online API and hides the dashboard when connectivity or authorization fails.

```powershell
Set-Location D:\Projects\Common.Security
$env:COMMON_SECURITY_CONNECTION = 'Data Source=D:\Projects\FFPMobile\artifacts\mobile-accounts.db'
dotnet run --project .\Common.Security.Tools -- import --provider sqlite --application ffp-mobile --manifest $approvedManifestPath --actor $approverId
dotnet run --project .\Common.Security.Tools -- import --provider sqlite --application ffp-mobile --manifest $approvedManifestPath --actor $approverId --apply
```

Use the admin commands below from the approved management network. curl prompts for the password when no password appears in `--user`. The list returns up to 100 devices. Supply the final returned ID as `?after=ID` to request the next page. The PUT route accepts `Approved`, `Suspended` and `Revoked`.

```powershell
curl.exe --fail-with-body --user $adminSamAccountName https://mobile.ffpja.org/api/admin/devices
curl.exe --fail-with-body --user $adminSamAccountName -X PUT "https://mobile.ffpja.org/api/admin/devices/$installationId/status/Approved"
```

## FFP Manager permission commissioning

The repository [FFPMobile.FfpManagerBridge](../services/FFPMobile.FfpManagerBridge) contains the outbound authorization adapter. Like RequestPortal's integration, it keeps ERP-specific details behind a separate service boundary. The mobile API owns authentication, device state, sessions and the stable capability contract. A Business Central provider can implement the same permission source.

The inspected `ffp` checkout defines `FFP.Module.Security.PermissionUser` (a `PermissionPolicyUser`) and runs the desktop application with `SecurityStrategyComplex` and secured XPO object spaces. Its user metadata lookup uses the legacy login name. A verified immutable AD-to-XAF association must therefore be commissioned. RequestPortal's installed bridge contract handles RequestPortal records. It has no endpoint that evaluates effective XAF warehouse permissions.

| Dashboard module | Decision ID | Existing XAF scope to commission |
| --- | --- | --- |
| Stock Counts | `stock-counts` | `CountSheet`, `CountSheetItem` and the approved stock count views |
| Inventory Reports | `inventory-reports` | Approved reports under the Inventory report category, report visibility, and secured read access for each report's `Inventory` or `DailyInventory` dataset |
| Delivery Orders | `delivery-orders` | `DeliveryOrder_ListView`, `DeliveryOrder` and `DeliveryOrderDetail` |
| Return to Stock | `return-to-stock` | `ReturnedOrder_ListView`, `ReturnedOrder` and `ReturnedItem` |
| Goods Receival | `goods-receival` | `GoodsReceivalNote_ListView`, `GoodsReceivalNote` and `GoodsReceivalNoteDetail` |

The ERP owners must add a read-only permission evaluator alongside the XAF deployment with access to the installed model, secured XPO adapter and existing effective permissions. Verify the active XAF user by `PermissionUser.Oid` and verified directory subject. Evaluate the desktop model's navigation, type, object, member and relevant report/action checks in that principal's security context. Approve the exact dashboard visibility criteria and Inventory report set. Honor restrictive criteria. Return a module only when the evaluator can establish its approved visibility. Future business screens require their own record-level and action checks. Never substitute raw role table queries, AD group guesses or inferred employee attributes.

The evaluator contract is HTTPS and requires the service bearer from OpenBao. A private origin is configured under `FfpManager:PermissionServiceOrigin`. The administrator configures each `FfpManager:Bindings:<CommonAccountId>` entry with approved `DirectoryId`, `SubjectId` and `UserId` (the XAF OID). On every check, the API requires the same Common.Security account and AD GUID pair used when the binding was commissioned.

- `POST /v1/warehouse/permissions` accepts `{"userId":"<xaf-oid>","directoryId":"<ad-domain-guid>","subjectId":"<ad-user-guid>"}`. On success return `{"userId":"<same-xaf-oid>","modules":["stock-counts"]}`, including an empty array when there are no grants. Use HTTP 403 or 404 for unresolvable identities. A mismatched response user, missing mapping, and unknown capability IDs cannot grant access.
- `GET /v1/warehouse/ready` returns 2xx only when the private evaluator can check the deployed XAF security model and data source. Use 503 when it cannot. Keep any JMDB01 access and connection credentials inside the evaluator's boundary and grant read-only access.

FFPMobile denies access when this evaluator, its bindings or its required decisions are unavailable. The evaluator must be commissioned and verified against representative authorized and denied XAF users before an employee can reach a live Warehouse dashboard.

## Implementation inventory

| Files | Changes |
| --- | --- |
| `services/FFPMobile.Api/{Program,Authentication,DeviceStore,WarehouseEndpoints}.cs`, API project and appsettings | LDAPS, account binding and session management, request proof, approval inventory, auditing and health |
| `services/FFPMobile.FfpManagerBridge/` | Stable Warehouse capabilities, verified XAF identity bindings and HTTPS evaluator client |
| `shared/WarehouseProtocol/` | Device and authentication contracts, signed HTTPS client and offline interfaces |
| `apps/FFPMobile.WarehouseOperations/` | Tablet sign-in, authorized dashboard, secure session storage, Android manifest and Keystore |
| `tests/FFPMobile.Api.Tests/` | API, client and bridge automation with test-only directory and evaluator substitutes |
| `deploy/kratos/`, `scripts/Deploy-FFPMobile-Kratos.ps1` | Private ingress, systemd, OpenBao, release installation and rollback |
| `.gitignore`, `.gitattributes`, `docs/` | Local artifact exclusions, Linux deployment file line endings and commissioning instructions |

## Validation

The .NET 11 API and signed Warehouse APK built on Windows with zero warnings and errors. The APK installed and cold-launched on an Android API 37 emulator. The login controls, Food For The Poor Jamaica branding and API-unavailable message were verified through the Android accessibility hierarchy. The framework-dependent Linux x64 publish also succeeded. A localhost test using trusted HTTPS returned HTTP 200 for liveness and HTTP 503 for readiness with commissioning settings absent.

All 18 automated tests passed. They cover authentication, disabled accounts, absent bindings, device approval and restrictions, all five module decisions, session expiry/revocation and rotation, logout, replay rejection, employee separation and API failure. Live AD, an approved XAF evaluator, internal DNS, approved employee credentials and an authorized warehouse tablet are required for organizational acceptance testing. See the [Kratos runbook](KRATOS-WAREHOUSE.md).
