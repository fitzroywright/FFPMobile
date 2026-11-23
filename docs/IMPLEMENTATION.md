# Implementation backlog and release gates

## What exists

Android MAUI app entrypoints, a shared preview login layout, three distinct module dashboards, unique Android package IDs, and baseline architecture documentation.

## Required before deployment

1. Verify build with installed .NET 11 Android + MAUI workloads on supported Windows/macOS agent. No build has been executed by this scaffold author.
2. Integrate actual approved FFP Manager authentication, without retaining passwords. Replace preview-only login.
3. Add server-resolved effective XAF security permissions and signed bounded offline authorization.
4. Add encrypted SQLite storage, persistent outbox and conflict-resolution sync protocols.
5. Implement fully working role-gated module screens, validation, attachments and barcode capture.
6. Build private FFP Integration Services with isolated gateway and ERP adapters, separate from Request Portal.
7. Test device enrollment/revocation, power loss, reconnect, retry/idempotency, authorization changes, inventory conflicts.
8. Conduct UAT, accessibility and tablet layout verification; sign binaries and configure device management.

**Do not publish prototype preview builds as production apps.**
