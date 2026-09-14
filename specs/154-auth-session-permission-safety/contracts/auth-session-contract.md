# Auth and Session Contract

## Access Token Claims

Issued access tokens for platform users must include:

- `nameidentifier`: user id
- `role`: one or more role names
- `passwordResetVersion`: current `User.PasswordResetVersion`
- `securityStampVersion`: current `User.SecurityStampVersion`
- `permission`: zero or more permissions derived from current roles

Validation result:

- Missing user id → unauthenticated.
- Missing required version claim → unauthenticated stale token.
- User not found or inactive → unauthenticated stale token.
- Version mismatch → unauthenticated stale token.

## Refresh Endpoint

Endpoint: `POST /api/auth/refresh`

Request:

- Refresh token is read from the existing request/cookie contract used by `AuthController`.
- Body remains `{}` for frontend callers.

Success response:

```json
{
  "success": true,
  "data": {
    "accessToken": "jwt",
    "refreshToken": "opaque-token-if-response-contract-keeps-it",
    "user": {
      "id": "guid",
      "fullName": "string",
      "phone": "string",
      "roles": ["Student"],
      "permissions": [],
      "profileComplete": true,
      "avatarSlug": null,
      "allowedDomains": [],
      "allowedNavbarItems": []
    }
  }
}
```

Failure responses:

- Invalid, replayed, expired, inactive user, stale account, or revoked device refresh → 401 with `success=false`.
- Refresh failure must not create a new refresh token.

## Forbidden Failure Contract

Authenticated-but-not-allowed application failures must return:

- HTTP status: 403
- JSON body compatible with `ApiResponse.Fail(message)`

Unauthenticated or stale-session failures must return:

- HTTP status: 401
- JSON body compatible with `ApiResponse.Fail(message)`

Frontend handling:

- 401 may clear auth state and redirect to login after refresh retry fails.
- 403 must not clear auth state.
