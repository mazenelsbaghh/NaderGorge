# Research Summary: Form ID Mismatch Fix

## Decision
Modify the frontend service functions in `frontend/src/services/forms-service.ts` to include the appropriate identifier field (`id` or `submissionId`) inside the request body payload of PUT requests.

## Rationale
- **The Issue**:
  - The backend endpoints `PUT /api/admin/forms/{id}` and `PUT /api/admin/forms/submissions/{submissionId}/status` validate that the GUID in the route path matches the GUID inside the request body:
    ```csharp
    if (id != command.Id) return BadRequest("Form ID mismatch");
    ```
  - Currently, the frontend helper functions in `forms-service.ts` call `apiClient.put` but omit the identifier field (`id` or `submissionId`) from the request payload.
  - As a result, ASP.NET Core deserializes the missing property as `Guid.Empty` (`00000000-0000-0000-0000-000000000000`), which causes a mismatch and triggers a `400 Bad Request` status code.
- **The Fix**:
  - Trivial and robust fix: Spread the form fields and inject `id` (e.g. `{ ...form, id }`) and `submissionId` in the respective Axios PUT request bodies.

## Alternatives Considered
- **Modifying the backend**: Removing the route path vs body verification check on the backend.
  - *Rejected because*: Having route vs body ID validation is a standard secure coding practice that prevents malicious clients from targeting or updating a different resource identifier than the one specified in the route.
