# FFP Mobile

Three independently deployable Android tablet applications, built with .NET MAUI:

- **FFP Mobile Field Officers** — field-work application.
- **FFP Mobile Warehouse & Operations** — purchase requisitions, payment requests, delivery orders, returns to stock, and inventory count sheets.
- **FFP Mobile Security Post** — gate and dispatch verification.

The applications share UI foundations and security/sync contracts, but each has its own Android application ID and release lifecycle.

## Status

Initial scaffold only. This repository does **not** yet provide a working ERP bridge, FFP Manager authentication, encrypted local persistence, or production offline synchronization. Authentication screens are **non-functional** until the secure identity integration is implemented; demonstration content must not be presented as live data.

## Security and integration rules

- FFP Manager effective XAF permissions are authoritative; never invent standalone mobile roles.
- No direct SQL Server connection from tablets.
- Mobile clients use a dedicated internal FFP Integration Services API (not Request Portal's internet-origin bridge).
- ERP-specific code remains inside replaceable FFP Manager / Business Central adapters.
- Transaction creation must eventually operate offline; server-side permission checks and posting only after synchronization.
- Never retain plain-text passwords or privileged API secrets on a tablet.

See `docs/ARCHITECTURE.md` and `docs/IMPLEMENTATION.md`.
