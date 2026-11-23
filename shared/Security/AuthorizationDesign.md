# Security contract (implementation required)

FFP Manager DevExpress XAF effective permissions (PermissionUser / PermissionPolicyRole) are the authority, including object-level criteria and denied permissions. Each Android app must obtain a signed, device-bound, expiring offline authorization snapshot after online authentication. UI visibility is not authorization. Backend must revalidate every synchronized operation against current ERP permissions. Guard access must be separately scoped and cannot inherit financial transaction rights. Do not store user passwords locally.
