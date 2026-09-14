# Historical Migration Bootstrap Audit

## Confirmed fixed identities

`20260613154904_AddIbrahimAdmin` previously inserted a fixed active Admin and a
tracked password hash. Its data operation is now intentionally empty for fresh
databases. Existing environments are cleaned by
`20260726120000_HardenProductionBootstrapData`; production creates its first
Admin only through the protected bootstrap tool.

`20260607200637_AddMultiTeacherSubjectArchitecture` requires temporary rows
while introducing historical non-null teacher/subject foreign keys. The
temporary user is inactive, has a non-authenticatable hash, and uses sentinel
identity values. The forward hardening migration deletes the exact known rows
when they are unreferenced.

## Dependency-safe cleanup

The historical teacher profile and subject can be referenced by content tables
created or transformed later in the migration chain. The forward cleanup:

1. removes the exact legacy teacher-subject relation;
2. attempts exact-ID deletes inside isolated PostgreSQL exception blocks;
3. retains a row if any approved data references it rather than cascading or
   failing the whole release;
4. relies on the clean-database acceptance gate to require their absence in the
   new Production database.

No wildcard, phone-only, name-only, or broad catalog deletion is permitted.

## Fresh Production assertions

- No users exist before the manual Admin bootstrap.
- The known historical Admin and teacher IDs do not exist.
- The known legacy teacher profile and subject IDs do not exist.
- No demo catalog code/name is present.
- Roles and structural reference data required by the application may exist,
  but no identity is seeded.
- `SeedDefaults:Enabled` is false in Production.
