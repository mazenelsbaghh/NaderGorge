# Contracts: Student Auth Redesign

## Endpoint: `POST /api/auth/register`

Accepts a single payload to create both the user identity and the academic profile atomically.

### Request Body (JSON)
```json
{
  "fullName": "Student Name",
  "phoneNumber": "01000000000",
  "password": "SecurePassword123",
  "parentPhone": "01111111111",
  "gradeId": "00000000-0000-0000-0000-000000000000",
  "trackId": null,
  "school": "Example High School",
  "governorate": "Cairo",
  "city": "Nasr City"
}
```

### Success Response (200 OK)
```json
{
  "success": true,
  "message": "User registered successfully",
  "data": {
    "token": "jwt.token.string"
  }
}
```

### Error Response (400 Bad Request)
```json
{
  "success": false,
  "message": "Validation failed",
  "errors": [
    "Phone number is already registered."
  ]
}
```
