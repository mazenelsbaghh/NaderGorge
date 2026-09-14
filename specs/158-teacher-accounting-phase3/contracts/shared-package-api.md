# Contract: Shared Multi-Teacher Package API

## Admin Endpoints

### POST `/api/admin/shared-packages`

Creates a shared package.

Request:

```json
{
  "name": "All Teachers Term Bundle",
  "slug": "all-teachers-term-bundle",
  "description": "Shared package for multiple teachers",
  "price": 1000.0,
  "distributionMode": "Mixed",
  "isPublished": false,
  "teachers": [
    {
      "teacherId": "guid",
      "subjectId": "guid",
      "allocationMode": "Percentage",
      "allocationValue": 30.0
    },
    {
      "teacherId": "guid",
      "subjectId": "guid",
      "allocationMode": "FixedAmount",
      "allocationValue": 250.0
    }
  ],
  "items": [
    {
      "teacherId": "guid",
      "contentType": "Package",
      "contentId": "guid",
      "subjectId": "guid"
    }
  ]
}
```

Validation:
- Package price must be positive.
- At least one teacher and one item are required.
- Percentage allocations cannot exceed 100.
- Fixed allocations cannot make platform remainder negative.
- Included content must belong to the selected teacher unless admin explicitly marks it platform-wide.

### PUT `/api/admin/shared-packages/{id}`

Updates draft/unpublished package details and allocation rules. Published package allocation changes require explicit versioning or future-effective update to protect historical financial events.

### POST `/api/admin/shared-packages/{id}/publish`

Publishes the package after validation.

### GET `/api/admin/shared-packages`

Returns paginated admin list with status, price, teacher count, item count, and allocation summary.

### GET `/api/admin/shared-packages/{id}`

Returns full editable detail.

## Student Endpoints

### GET `/api/student/shared-packages`

Returns published shared packages available to the student.

### GET `/api/student/shared-packages/{slugOrId}`

Returns package detail with teachers, subjects, included content preview, price, and purchase eligibility.

### POST `/api/student/shared-packages/{id}/purchase`

Purchases the shared package using the existing purchase/balance/discount flow and creates:
- student access grant(s),
- one teacher financial event,
- one allocation per configured teacher,
- platform remainder in the event.

Response data:

```json
{
  "purchaseOperationId": "guid",
  "sharedPackageId": "guid",
  "paidAmount": 1000.0,
  "teacherAllocations": [
    {
      "teacherId": "guid",
      "teacherShareAmount": 300.0
    }
  ],
  "platformShareAmount": 700.0
}
```
