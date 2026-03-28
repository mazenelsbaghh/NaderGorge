# Testing API Contracts

To write resilient tests, the backend API should expose a set of backdoor endpoints **ONLY** active when `ASPNETCORE_ENVIRONMENT=E2e`. These endpoints are used by Playwright's `APIRequestContext` to perform rapid setup and teardown of the test database.

## 1. Test Seed Endpoint
`POST /api/e2e/seed`

**Request:**
```json
{
  "ClearDatabase": true,
  "SeedAdmin": true,
  "SeedStudents": true
}
```
**Response:** `200 OK`
```json
{
  "AdminToken": "ey...",
  "StudentToken": "ey..."
}
```

## 2. Test Package Creator Endpoint
To set up content consumption tests without explicitly running through the admin UI each time (except for the admin UI test itself).

`POST /api/e2e/setup-mock-package`

**Response:** `200 OK`
```json
{
  "PackageId": "uuid",
  "LessonId": "uuid",
  "VideoId": "uuid"
}
```

## Universal OTP Feature
When `E2e` environment is active, the regular login route natively bypasses correct OTP checking if the provided OTP is `"000000"`, and bypasses SMS sending altogether.
