# Quickstart: Package Profile Development

## Development Prerequisites
- Docker container for DB running (Postgres).
- Next.js development server running.
- Backend .NET server running.

## Local Test Setup
1. Log in with an Admin account (`01000000000` / `Admin@123`).
2. Navigate to `<host>/admin/content`.
3. Locate an existing package from the list and hover over it. The "Manage Package" (or view profile) button should route you to `/admin/content/packages/[id]`.

## Validation Protocol
You can independently test this capability by sending a cURL POST to the `TermsController` utilizing the `PackageId` extracted from the URL path.
Ensure the Bearer Token used belongs to an Admin.
