# Quickstart & Verification: 116-performance-deep-remediation

## Local Setup

1. Run infrastructure services:
   ```bash
   make up
   ```

2. Run the frontend in development mode or build it:
   ```bash
   cd frontend
   npm run build
   ```

3. Run backend tests:
   ```bash
   dotnet test backend/NaderGorge.sln
   ```

## Verification Checks

1. **Verify Static Pages**:
   Verify that public pages are static by running:
   ```bash
   cd frontend
   npm run build
   ```
   Check the output console for `○` (Static) symbols next to public routes (`/`, `/about`, `/faq`, `/login`, `/register`).

2. **Verify Asset Sizes**:
   Check if logo SVG files are under 50KB:
   ```bash
   find frontend/public/images -name "*.svg" -exec ls -lh {} +
   ```

3. **Verify Shell API Waterfall**:
   Open browser dev tools on the Student dashboard and verify only a single `/api/student/shell-bootstrap` request is fired on mounting the page.
