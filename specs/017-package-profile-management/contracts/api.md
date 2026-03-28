# API Contracts: Package Profile and Term Management

The frontend uses Next.js server components and client queries (TanStack React Query) talking natively to the API abstraction layer in `frontend/src/services/curriculum-service.ts` or `frontend/src/services/admin-service.ts`. The backend exposes these natively in `NaderGorge.API`.

## 1. Get Package Details
**Endpoint**: `GET /api/curriculum/packages/{packageId}`
**Role**: Admin, Student (Admin gets more metadata)
**Request Path Parameters**:
- `packageId` (Guid, required)

**Response (Success - 200 OK)**:
```json
{
  "id": "guid",
  "title": "الصف الثالث الثانوي - باقة الفيزياء الأساسية",
  "description": "...",
  "price": 500,
  "isActive": true,
  "terms": [
    {
      "id": "guid",
      "packageId": "guid",
      "title": "الترم الأول",
      "order": 1,
      "sections": [...] 
    }
  ]
}
```

## 2. Update Package Details
**Endpoint**: `PUT /api/curriculum/packages/{packageId}`
**Role**: Admin
**Request Body**:
```json
{
  "title": "الصف الثالث الثانوي - باقة الفيزياء الأساسية (معدل)",
  "description": "...",
  "price": 500,
  "isActive": true
}
```
**Response (Success - 200 OK)**

## 3. Create Term within Package
**Endpoint**: `POST /api/curriculum/packages/{packageId}/terms` (or `POST /api/curriculum/terms` with `packageId` in body)
**Role**: Admin
**Request Body**:
```json
{
  "packageId": "guid",
  "title": "الترم الأول",
  "order": 1,
  "isActive": true
}
```
**Response (Created - 201 Created)**:
Returns the created Term object.
