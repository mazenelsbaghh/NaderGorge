# Research: تحديث نموذج تسجيل الطالب

**Feature**: 016-registration-form-updates
**Date**: 2026-03-28

## Research Topics

### R1: Governorate-District Data Source

**Decision**: Use static hardcoded data in the frontend (TypeScript map).

**Rationale**:
- The governorate list is already hardcoded in `RegistrationForm.tsx` (27 governorates).
- Districts/neighborhoods per governorate are relatively stable — they don't change frequently.
- No API call needed — instant load (meets SC-004: < 1 second).
- Can be migrated to a database-backed API later without frontend changes (just swap the import).

**Alternatives considered**:
- Database table with API endpoint: Overkill for stable reference data. Adds latency and a backend dependency for the registration flow.
- External API (e.g., Egypt postal API): No reliable, free Egyptian district API exists. Would add fragility.

### R2: StudentCode Field — Removal Strategy

**Decision**: Make StudentCode optional at all layers. Do NOT delete the column.

**Rationale**:
- Existing students already have student codes in the DB. Deleting the column would lose data.
- The EF Core migration will: `ALTER COLUMN "StudentCode" DROP NOT NULL` and set a default of `""`.
- Backend `RegisterCommand` will change `string StudentCode` to `string? StudentCode` and remove the `NotEmpty()` validator.
- Frontend will stop sending `studentCode` in the registration payload.
- Admin panel will still display studentCode for legacy students who have one.

**Alternatives considered**:
- Hard delete the column: Violates backward compatibility and loses historical data.
- Keep it required with a default: Forces meaningless data into a field that no longer has purpose.

### R3: Dual Phone Number Architecture

**Decision**: Add `SecondaryPhone` and `SecondaryParentPhone` as nullable string fields on `StudentProfile`.

**Rationale**:
- Simple, flat schema. No need for a separate phone number table — each entity has at most 2 phones.
- Nullable because the secondary numbers are optional.
- Same validation rules as primary phones (Egyptian phone format: `^01[0125]\d{8}$`).
- No uniqueness constraint on secondary phones (multiple students may share a family phone).

**Alternatives considered**:
- Separate `PhoneNumbers` table with type enum: Over-engineered for a max of 4 phone numbers per student. Would require joins on every query.
- JSON array field: Harder to query and validate. PostgreSQL supports it but adds complexity.

### R4: District/Neighborhood Data Coverage

**Decision**: Include major districts for all 27 Egyptian governorates. For governorates with fewer defined urban districts, include the major cities/areas.

**Rationale**:
- Students registering need to pick a recognizable area name.
- Complete coverage ensures no student is left with an empty district.
- The data is organized as `Record<string, string[]>` mapping governorate name → ordered list of districts.

**Data source**: Egyptian administrative divisions researched from Wikipedia, official gov.eg portals (cairo.gov.eg, sharkia.gov.eg, dakahliya.gov.eg, etc.), and Marefa.

**Implementation**: File created at `frontend/src/data/governorate-districts.ts` with:
- **Urban governorates** (Cairo, Alexandria, Port Said, Suez): أحياء (neighborhoods)
- **Rural governorates** (Delta, Upper Egypt): مراكز (centers) + major مدن (cities)
- **Border governorates** (Matrouh, Red Sea, Sinai, New Valley): مدن (towns)
- **New cities** included where applicable (e.g., القاهرة الجديدة، 6 أكتوبر، مدن جديدة)
- Total: 27 governorates, ~270 districts/neighborhoods

### R5: Migration Strategy (Docker PostgreSQL)

**Decision**: Standard EF Core migration. `dotnet ef migrations add AddRegistrationFieldUpdates` then `dotnet ef database update` inside the Docker container.

**Rationale**:
- The project already uses EF Core code-first migrations (constitution VII mandates this).
- The Docker PostgreSQL DB is accessed through the standard connection string.
- All changes are additive (new nullable columns) or relaxing constraints (StudentCode → optional) — no destructive changes.

**Migration details**:
1. Add columns: `District` (varchar 200, nullable), `SecondaryPhone` (varchar 20, nullable), `SecondaryParentPhone` (varchar 20, nullable)
2. Alter column: `StudentCode` — remove `NOT NULL` constraint, set default `""`
3. No data migration needed — existing rows will have NULL for new columns and keep their existing StudentCode values

## Unresolved Items

None — all research topics resolved.
