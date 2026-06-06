# Data Model: Brand Identity Migration

## Entity Schema & Configuration

No new database tables or schema migrations are required for this feature, as it is a visual and text rebranding.

The following configurations will be updated:

### 1. UI Styling Tokens
The CSS variables in `globals.css` will be mapped as follows:
- `--primary`: `#0A1D3D` (Deep Navy)
- `--primary-strong`: `#021f45`
- `--primary-container`: `#0E8F8F` (Teal)
- `--primary-foreground`: `#ffffff`
- `--accent`: `#D4A017` (Warm Gold)

### 2. Next.js Metadata
Metadata in `layout.tsx` and static page templates:
- Title: "Massar Academy | مسار أكاديمي"
- Description: "مسار أكاديمي - منصة تعليمية متكاملة لرحلة نجاحك"
