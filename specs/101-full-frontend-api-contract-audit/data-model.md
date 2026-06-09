# Data Model: Full Frontend API Contract Audit

## FrontendEndpointContract

Represents one API call made by current frontend code.

**Fields**:

- `method`: HTTP method inferred from `apiClient`, `axios`, or `fetch`.
- `path`: Normalized path including `/api` when it targets the backend.
- `routeTemplate`: Original normalized path template for display.
- `source.file`: Frontend file that contains the call.
- `source.line`: Line number of the call.
- `origin`: `backend-api`, `next-api`, `worker-api`, or `external`.
- `queryParameters`: Query-string names detected from literal query strings or Axios `params` objects.
- `payloadHint`: The argument or variable name passed as request body when statically visible.
- `rawExpression`: Original string/template expression used by frontend code.

## BackendRouteContract

Represents one ASP.NET Core controller endpoint.

**Fields**:

- `method`: HTTP method from `[HttpGet]`, `[HttpPost]`, `[HttpPut]`, `[HttpPatch]`, or `[HttpDelete]`.
- `path`: Normalized route path including `/api`.
- `routeTemplate`: Original combined controller/action route template.
- `controller`: Controller class name.
- `action`: Action method name.
- `authorization`: `anonymous`, `authorized`, `internal-token`, or `e2e-token`.
- `source.file`: Backend controller file.
- `source.line`: Attribute line number.

## ContractFinding

Represents a contract mismatch found by the audit.

**Fields**:

- `severity`: `P1` for missing route/method drift, `P2` for unsupported field/response drift, `P3` for documentation-only gaps.
- `kind`: `missing-backend-route`, `method-mismatch`, `ambiguous-route`, `payload-field-mismatch`, `response-field-mismatch`, or `intentional-exception`.
- `message`: Human-readable finding.
- `frontend`: Frontend endpoint contract reference.
- `backendCandidates`: Nearby backend route contracts when available.
- `status`: `open`, `fixed`, or `documented-exception`.

## Validation Rules

- Every `backend-api` frontend endpoint must match exactly one backend route by method and normalized route template.
- `next-api`, `worker-api`, and `external` frontend endpoints are inventoried but excluded from backend route failure counts.
- Query parameters are reported for audit visibility; they become blocking only when an exact backend action parameter mismatch is identified during remediation.
