# Content Pricing API Contracts

These contracts govern the communication between the Next.js admin frontend and the ASP.NET Core API for content creation with pricing.

## DTOs & Payloads

### POST `/api/admin/terms`
**Request Payload**:
```json
{
  "packageId": "UUID",
  "title": "Summer Revision",
  "order": 1,
  "price": 50.00
}
```

### POST `/api/admin/sections`
**Request Payload**:
```json
{
  "termId": "UUID",
  "title": "Unit 1: Algebra",
  "order": 1,
  "price": 20.00
}
```

### POST `/api/admin/lessons`
**Request Payload**:
```json
{
  "contentSectionId": "UUID",
  "title": "Quadratic Equations",
  "summary": "Full review",
  "order": 1,
  "price": 10.00
}
```

*(Note: Read Models / GET requests will simply echo these fields back in their responses under the `price` key).*
