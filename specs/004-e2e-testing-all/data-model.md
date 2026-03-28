# E2E Testing Data Model and Entities

Because this is a testing infrastructure feature rather than an application-level feature, it does not modify the core `NaderGorge.Domain` database schemas. Instead, it relies on ephemeral test entities.

## Entities

### Test User Accounts
- **Admin**: An account seeded specifically to have the `Admin` role. Used exclusively for testing content creation and code generation. `+20000000000`.
- **Student (Clean)**: An account seeded for login and basic usage. `+20000000001`.
- **Student (Device Mux)**: Used to test device limit triggers. `+20000000002`.

### Isolated Databases
- PostgreSQL: `nadergorge_e2e` (created/migrated/cleared via `npx playwright test` global setup or custom pre-test script).
- Redis: `localhost:6379/1` (Database index 1 instead of 0 to isolate keys).
