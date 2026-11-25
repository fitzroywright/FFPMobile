# Implementation backlog and release gates

The current Android deliverables are the login and settings interfaces, isolated preview dashboards and module navigation, plus the private API client and warehouse offline infrastructure.

## Before deployment

1. Commission the approved Common.Security provider and the effective FFP Manager XAF permission adapter. Integrate the Kotlin client with the Warehouse API for tablet enrollment, signed requests, session lifecycle and logout before enabling credential entry.
2. Define the server contract for app-, account- and device-bound offline permissions. Provision the verification key and trusted time, define refresh and revocation, and enforce capabilities per action.
3. Specify warehouse payloads, the idempotency and status API, retryable failure mapping, conflict resolution and audit rules. Wire the live session provider and transport. Verify duplicate submission, reconnect, power loss, revocation and conflict handling against a staging server.
4. Agree field workflows, attachments and assigned-work rules. Design Security Post checkpoints and online authorization for physical release with operations staff.
5. Finish audited production screens, signed releases, device management and UAT, including authorization changes and tablet accessibility.
