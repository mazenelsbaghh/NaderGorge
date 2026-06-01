# API Contract: POST /api/auth/register

**Version**: v2 (025-student-profile-v2)  
**Endpoint**: `POST /api/auth/register`  
**Auth**: None (public)

---

## Request Body (RegisterCommand)

```json
{
  "fullName": "أحمد محمد محمود علي",
  "phoneNumber": "01012345678",
  "secondaryPhone": "01112345678",
  "password": "password123",
  "dateOfBirth": "2005-03-15T00:00:00Z",
  "gender": "Male",
  "nationality": "مصري",
  "governorate": "القاهرة",
  "district": "مدينة نصر",
  "address": "15 شارع المحطة",
  "parentPhone": "01212345678",
  "secondaryParentPhone": "01312345678",
  "motherPhone": "01512345678",
  "isFatherAlive": true,
  "isMotherAlive": true,
  "fatherDateOfBirth": "1970-06-20T00:00:00Z",
  "motherDateOfBirth": "1975-09-10T00:00:00Z",
  "schoolName": "مدرسة الاتحاد اللغات",
  "schoolType": "Language",
  "educationStage": "Secondary",
  "gradeLevel": "FirstSecondary",
  "studyTrack": null
}
```

### Field Reference

| Field | Type | Required | Notes |
|---|---|---|---|
| `fullName` | string | ✅ | min 4 parts separated by spaces, Arabic |
| `phoneNumber` | string | ✅ | Regex: `^01[0125]\d{8}$` |
| `secondaryPhone` | string? | ❌ | Same regex if provided |
| `password` | string | ✅ | Min length 8 |
| `dateOfBirth` | ISO8601 | ✅ | Must be in the past |
| `gender` | `"Male"\|"Female"` | ✅ | |
| `nationality` | string? | ❌ | **NEW** — one of the 22 Arab nationalities |
| `governorate` | string | ✅ | One of 27 Egyptian governorates |
| `district` | string | ✅ | Neighborhood/area |
| `address` | string | ✅ | |
| `parentPhone` | string | ✅ if isFatherAlive | رقم هاتف الأب — required if alive |
| `secondaryParentPhone` | string? | ❌ | |
| `motherPhone` | string? | ❌ | **NEW** — رقم هاتف الأم |
| `isFatherAlive` | bool | ✅ | default: true |
| `isMotherAlive` | bool | ✅ | default: true |
| `fatherDateOfBirth` | ISO8601? | ❌ | **NEW** |
| `motherDateOfBirth` | ISO8601? | ❌ | **NEW** |
| `schoolName` | string? | ❌ | **NEW** |
| `schoolType` | string? | ❌ | **NEW** — `"Government"\|"Language"\|"Experimental"\|"Private"\|"Azhari"\|"American"` |
| `educationStage` | string | ✅ | `"Secondary"\|"Baccalaureate"\|"Primary"\|"Preparatory"\|"Azhari"\|"American"` |
| `gradeLevel` | string | ✅ | See GradeLevel enum — must match stage |
| `studyTrack` | string? | conditional | Required for `SecondSecondary` and `SecondBaccalaureate` |

---

## Response

### 200 OK

```json
{
  "success": true,
  "data": {
    "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "message": "Registration successful. Please log in."
  }
}
```

### 400 Bad Request (Validation Error)

```json
{
  "success": false,
  "message": "Full name must contain at least 4 parts (الاسم رباعي)"
}
```

### 409 Conflict (Duplicate Phone)

```json
{
  "success": false,
  "message": "رقم الهاتف الأساسي مسجل بالفعل. استخدم رقمًا آخر."
}
```

---

## Backward Compatibility

- All new fields (`nationality`, `motherPhone`, `fatherDateOfBirth`, `motherDateOfBirth`, `schoolName`, `schoolType`) are **nullable/optional**
- Existing registrations without these fields remain valid
- `parentPhone` conditional requirement: only required when `isFatherAlive = true` (unchanged behavior — previously always required; new behavior aligns with spec)
