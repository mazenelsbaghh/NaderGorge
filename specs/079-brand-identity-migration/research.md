# Research: Brand Identity Migration

## Decision

1. **Typography**:
   - Replace Cairo font with **Tajawal** (for Arabic) and **Montserrat** (for English).
   - Load these fonts in the Next.js `layout.tsx` using Google Fonts from `next/font/google`.
   - Update CSS classes in `globals.css` and Tailwind configuration.

2. **Colors**:
   - Primary: Deep Navy `#0A1D3D` (replacing the primary Gold `#9a6933`).
   - Accent: Teal `#0E8F8F` (replacing the container/secondary gold tokens).
   - Highlight/Warning: Warm Gold `#D4A017` / `#cb951e`.
   - Update variable definitions in `globals.css` (e.g., `--primary`, `--primary-strong`, etc.) so that all existing layout references reflect the change automatically.

3. **Logo & Favicon**:
   - Copy the new `logo.svg` to `frontend/public/assets/logo.svg` (and create a favicon version).
   - Update the logo component `Logo.tsx` (or similar references in header/footer) to render the new SVG.

## Rationale

- Tajawal offers superior readability for Arabic, matching the modern geometric structure of Montserrat for English.
- Re-using existing CSS color variables prevents having to manually rewrite hundreds of components. We only update the central styling tokens.

## Alternatives Considered

- Hardcoding new color classes like `bg-deep-navy` everywhere: Rejected because it violates DRY and increases development complexity. Modifying the Tailwind variable mapping is much simpler and safer.
