# Internal UI Data Model

- **ThemeTokenSet**: complete named semantic values for each supported mode; no optional status/surface members.
- **RouteInventoryEntry**: route pattern, role surface, authentication/permission state, dynamic parameters, owning component, and light/dark evidence.
- **DesignColorAllowlistEntry**: exact source location, allowed expression, rationale, owner, and review date.
- **PrimitiveVariant**: semantic variant contract for shared controls and state containers; must not require raw palette overrides.

No persistent data, API, backend, worker, or database model changes are allowed.
