# Data Model: Package-Specific Code Page Profiles

## Overview

This feature adds a package-scoped presentation aggregate that customizes the student code-redemption page for a specific package while preserving the existing `Package` entity as the source of academic/commercial identity.

## Entities

### Package

**Existing entity**: `NaderGorge.Domain.Entities.Package`

**Role in this feature**:
- Owns zero or one `PackageCodePageProfile`
- Continues to provide canonical package identity and commerce fields
- Remains the fallback source for package name, description, price, and availability state

**Relevant existing fields**:
- `Id`
- `Name`
- `Description`
- `Price`
- `IsActive`
- `ProgramId`

### PackageCodePageProfile

**New aggregate**

Represents package-specific copy and presentation settings for the code-redemption page.

**Proposed fields**:

| Field | Type | Rules |
|-------|------|-------|
| `Id` | Guid | Primary key |
| `PackageId` | Guid | Required, unique, FK to `Package` |
| `Status` | enum | Required; `Draft`, `Published`, `Fallback` |
| `HeroEyebrow` | string | Optional in draft, max 80 chars |
| `HeroTitle` | string | Required for publish, max 140 chars |
| `HeroDescription` | string | Required for publish, max 600 chars |
| `OfferTitle` | string | Required for publish, max 120 chars |
| `OfferDescription` | string | Required for publish, max 600 chars |
| `ActivationTitle` | string | Required for publish, max 120 chars |
| `ActivationDescription` | string | Required for publish, max 500 chars |
| `SupportTitle` | string | Optional, max 120 chars |
| `SupportDescription` | string | Optional, max 400 chars |
| `ThemeAccentKey` | string | Optional, bounded to supported presentation presets |
| `UpdatedByUserId` | Guid? | Last admin editor |
| `PublishedAt` | DateTime? | Set only when status becomes `Published` |
| `CreatedAt` | DateTime | Standard base entity field |
| `UpdatedAt` | DateTime | Standard base entity field |

**Behavior**:
- `Draft`: admin can continue editing; student page ignores custom fields and uses default fallback.
- `Published`: student page uses the custom profile for the package.
- `Fallback`: explicit reset state; the package uses the generic default page.

## Read Models

### AdminPackageCodePageProfileDto

Used by admin edit UI.

**Fields**:
- `packageId`
- `packageName`
- `status`
- all editable profile fields
- `isUsingFallback`
- `lastUpdatedAt`
- `publishedAt`

### StudentPackageCodePageDto

Used by the package-specific student code page.

**Fields**:
- `packageId`
- `packageName`
- `packageDescription`
- `packagePrice`
- `isPackageActive`
- `isUsingCustomProfile`
- `hero`
- `activationPanel`
- `offerPanel`
- `supportPanel`

This read model is composed from:
- `Package`
- published `PackageCodePageProfile` when available
- default fallback copy when no published profile exists

## Relationships

```text
Package 1 ─── 0..1 PackageCodePageProfile
User    1 ─── 0..* PackageCodePageProfile (via UpdatedByUserId audit reference)
```

## Validation Rules

### Publish validation

The profile can only move to `Published` when:
- parent package exists
- parent package is administratively addressable
- `HeroTitle` is present
- `HeroDescription` is present
- `OfferTitle` is present
- `OfferDescription` is present
- `ActivationTitle` is present
- `ActivationDescription` is present

### Save validation

- Draft saves may contain partial content
- All text fields must respect max lengths
- `ThemeAccentKey` must match one of the supported student-page presets
- Reset moves the profile to `Fallback` and clears student-visible custom copy

## State Transitions

```text
Fallback -> Draft
Draft -> Published
Published -> Draft
Published -> Fallback
Draft -> Fallback
```

Rules:
- `Fallback` means the generic code page is active
- `Draft` preserves in-progress admin edits without affecting student output
- `Published` is the only state that activates package-specific rendering

## Migration Notes

- Add a new table for `package_code_page_profiles`
- Add a unique index on `PackageId`
- Add FK from `PackageCodePageProfile.PackageId` to `packages.id`
- Add FK from `UpdatedByUserId` to `users.id` with nullable behavior

## Non-Goals

- No arbitrary layout builder
- No package commerce changes
- No content unlock logic changes
- No worker/queue involvement
