# Data Model: Rutube Video Provider

## Entities

### LessonVideo (Existing Entity)

We are simply augmenting an existing column. No new tables are required.

- **Entity Name**: `LessonVideo` (PostgreSQL)
- **Field**: `Provider` (String / Enum)
- **Modification**: `rutube` must be supported as a known provider for deserialization and business validation.

## API Contracts

### Internal Proxy API (`/api/video/embed`)

- **Method**: `GET`
- **Path**: `/api/video/embed`
- **Query Params**:
  - `vid`: String (The Rutube Video ID)
  - `provider`: String (Must equal `"rutube"`)

**Response**: HTML Document containing a Rutube Iframe structure configured with our internal `window.parent.postMessage` translation hub.
