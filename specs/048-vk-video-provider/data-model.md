# Data Model: VK Video Provider

## Entities

### 1. `LessonVideo` (Existing C# Entity)
*   **Modifications:**
    *   The `Provider` string enumeration must be updated to accept `"vk"` instead of `"telegram"` or `"google_drive"`.
    *   The `VideoUrl` field will now structurally expect a valid VK embed link (`https://vk.com/video_ext.php?oid=...`).

### Validation Rules
*   If a `LessonVideo` is submitted with `Provider = "vk"`, the `VideoUrl` MUST contain `vk.com/video_ext.php`, otherwise it is rejected.
*   Legacy rows with `Provider = "telegram"` or `"google_drive"` need to be handled carefully by the DBA or dropped in a cleanup migration.
