# Data Model: E2E Verification Fixtures

**Date**: 2026-03-27

## Objective
To map the primary data dependencies the Playwright tests must manipulate to interact fully with Phase 8 components.

## Required Setup Data Models

### 1. `AdminTestContext`
* **Role**: Authenticated administrator capable of spawning code groups.
* **Fields**: Valid Phone, Auth Token, Granted Role "Admin".

### 2. `CodeGroupFixture`
* **Role**: Tracks generated codes per test suit to ensure isolated code redemption attempts.
* **Fields**:
    * `group_id` (UUID): Reference ID to clean up.
    * `codes` (Array of Strings): The 6-20 character strings outputted by `BulkGenerateCodes`.
    * `code_type` (Enum): E.g., `Balance`, `Package`.
    * `encoded_value`: 500 (EGP) or matching UUID of `Package`.

### 3. `StudentTestContext`
* **Role**: A simulated student journey object tracking their metadata.
* **Fields**:
    * `phone`: `011999` + `timestamp` (Unique per run).
    * `national_id`: Randomly generated 14 chars.
    * `password`: Test string.
    * `balance` (Integer): Verified via UI and API intercepts.

## Test Boundaries
Each E2E suite will be isolated. The `StudentJourney` test will instantiate a StudentTestContext, login, fetch a Code from the `CodeGroupFixture`, redeem it, and verify the `StudentTestContext.balance` matches `CodeGroupFixture.encoded_value`.
