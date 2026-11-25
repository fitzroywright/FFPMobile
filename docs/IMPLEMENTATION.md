# Implementation backlog and release gates

All three Android clients support isolated Common.Security Mock sign-in and app-specific demo dashboards. Their online AD flows register independent Keystore installations and require administrator approval and an active organizational binding. Warehouse uses the FFP Manager bridge contract. Field Officers and Security Post stop at permission verification until their authoritative evaluator contracts are commissioned.

## Before deployment

1. Commission the effective FFP Manager XAF permission adapter for Warehouse. Define and commission app-specific permission decisions for Field Officers purchase orders and payment requests and for Security Post dispatch verification, vehicle movements and the visitor log. Reuse the approved Common.Security account binding and device approval flow.
2. Define the server contract for app-, account- and device-bound offline permissions. Provision the verification key and trusted time, define refresh and revocation, and enforce capabilities per action.
3. Specify warehouse payloads, the idempotency and status API, retryable failure mapping, conflict resolution and audit rules. Wire the live session provider and transport. Verify duplicate submission, reconnect, power loss, revocation and conflict handling against a staging server.
4. Agree field workflows, attachments and assigned-work rules. Design Security Post checkpoints and online authorization for physical release with operations staff.
5. Finish audited production screens, signed releases, device management and UAT, including authorization changes and tablet accessibility.
