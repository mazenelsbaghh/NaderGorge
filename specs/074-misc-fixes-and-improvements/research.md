# Research Notes: Miscellaneous Fixes and Improvements

This document details the architectural decisions and technical findings for the fixes and features.

## 1. QR Code Scanner Base URL

- **Decision**: Introduce `NEXT_PUBLIC_APP_URL` in the frontend (loaded at build/runtime in Next.js). Update `QrDisplay.tsx` to use this environment variable as the base URL if defined.
- **Rationale**: When the admin is logged in locally at `http://0.0.0.0:8738` or `http://localhost:8738`, `window.location.origin` resolves to these loopback addresses. The QR code must instead point to the public domain (e.g. `https://nadergeorge.com`) so that scanning devices (phones) can reach the server.
- **Alternative**: Resolving the server IP dynamically. Rejected because local development IP detection is unreliable on host machines running containerized stacks.

## 2. Admin Cancel Student Package

- **Decision**:
  - Extend `StudentPackageDto` to include `AccessGrantId`, `IsActive`, `PurchaseMethod`, and `Price`.
  - Modify `GetStudentProfileDetailQuery` to fetch all packages (active & inactive) and determine if they were bought via `Code` (where `AccessCodeId != null`) or `Balance` (where `AccessCodeId == null`).
  - Create `CancelPackageGrantCommand` to deactivate the grant and optionally refund the package price to `StudentBalance`.
  - Add a confirmation modal in the admin page `/admin/users/[id]` displaying the warning and prompting for a refund.

## 3. Account Disabling Reason & Arabic Message

- **Decision**:
  - Add `SuspensionReason` (nullable string) to the `User` domain model and run EF Core migration to update the schema.
  - Modify `ToggleStudentSystemAccessCommandHandler` to persist the reason directly to the database.
  - Modify `LoginCommandHandler` to output a clean, translated exception message containing the custom suspension reason and the support contact number `01272629122`.
  - Prompt the admin for a suspension reason when disabling a user in the UI.

## 4. Sidebar/Navbar Expand-on-Hover

- **Decision**: Use Tailwind CSS group selectors (`group/sidebar` on the `aside`) and state transitions (`transition-all duration-300 w-20 hover:w-60`) to expand the sidebar and reveal text labels next to the icons.
- **Rationale**: Keeps the collapsed sidebar compact on small screens and collapsed view, but expands dynamically to provide desktop navigation text tags without layout shifts (as it is fixed-position).

## 5. Redirect Logged-in Users from Login

- **Decision**: In `login/page.tsx`, check `isAuthenticated` and `isLoading` from Zustand `useAuthStore` and call `router.replace` to redirect them to `/admin` or `/student` depending on their role. Hide the login form with a loading spinner while redirecting.

## 6. Balance Edit Button Alignment

- **Decision**: Pass the edit button as a child of the `AdminStatCard` in the financials tab instead of overlaying it using absolute positioning. The child button will be styled with `flex items-center gap-2 px-4 py-2` to align the icon and text horizontally.

## 7. Rate Limit Increase (429)

- **Decision**: Modify `RateLimitingConfig.cs` in `NaderGorge.API` to increase:
  - Auth policies limit to 30 requests/min.
  - Code policies limit to 20 requests/min.
  - Video session policies limit to 30 requests/min.
  - Global partitioned rate limiter to 1000 requests/min.
