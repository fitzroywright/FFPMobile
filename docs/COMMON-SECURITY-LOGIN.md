# Common.Security login integration

The Android applications use one shared login client and call **FFPMobile.Api** over HTTPS.
The API references `Common.Security` 2.0.0 and uses its `LocalDatabaseAuthenticator`,
`PlatformAuthenticator`, `IAccountStore`, account status/revocation checks and
`OpaqueAccountSessions` for server-issued, expiring sessions.

**This is a security-first scaffold, not a commissioned authentication service.** The
default `IMobilePermissionResolver` denies every application. Replace it with a service
that evaluates effective **FFP Manager DevExpress XAF** permissions using a verified
immutable account binding. Do not infer permission from names or create separate roles.

The API only uses the LocalDatabase provider as a compile-time integration example;
a deployment must choose an approved Common.Security provider and establish approved
account records/identity links. No sample credentials or mock users exist. The Android
apps do not consume Common.Security assemblies directly because provider authentication,
directory access and account sessions belong on the trusted server.

## Before enabling sign-in

- Confirm Common.Security 2.0.0 package availability and trusted private NuGet feed.
- Supply a protected server-side `Security:SqlitePath` with restricted file permissions
  and an established account store. The API fails startup when it is absent.
- Replace `DenyAllMobilePermissionResolver` with approved FFP Manager permission integration.
- Configure HTTPS at a private ingress and block public exposure.
- Integrate a supported Common.Security authentication provider and approval lifecycle.
- Implement secure device enrollment, token handling, logout, signed bounded offline unlock,
  and server-side authorization for every transaction.
- Validate rate limiting, account lockout, audit events, key management and diagnostics.
- Build and test Android plus server on .NET 11. No compilation/testing has been executed here.

**Do not deploy this login scaffold to production.**
