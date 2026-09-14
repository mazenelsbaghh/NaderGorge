# Current Session Contract

## Endpoint

`GET /api/auth/session`

Requires the existing authenticated JWT. Returns HTTP 200 with the project `ApiResponse` envelope:

```json
{
  "success": true,
  "data": {
    "user": {
      "id": "uuid",
      "fullName": "string",
      "phone": "string",
      "roles": ["Admin"],
      "permissions": ["users.manage"],
      "profileComplete": true,
      "avatarSlug": null,
      "allowedDomains": ["admin"],
      "allowedNavbarItems": ["/admin/users"]
    },
    "authorizationVersion": 4,
    "serverTime": "2026-07-12T00:00:00Z"
  }
}
```

The endpoint is read-only and does not rotate refresh tokens. HTTP 401 means the session is no longer authenticated. It must never return a stale permission snapshot from a frontend cache.
