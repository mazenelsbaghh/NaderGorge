# Data Model Updates: Papyrus Package UI

*Note: This feature is purely a cosmetic UI update for the `PackageCard` components. It does not introduce any new tables or change the schema.*

## Existing Entities Addressed

### `Package` (Frontend Representation)

We will rely on existing properties from the frontend package interface (e.g., `PackageDto` or similar):
- `imageUrl`: string (Nullable) - This will be used to display the package image. If null/undefined, the fallback image `public/images/default-package.png` will be used instead.
- `title`: string
- `description`: string

No changes to the backend database are required.
