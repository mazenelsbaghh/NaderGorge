# Research: Package Partial Enrollment Display

## Decision
Pre-fetch user roles, active access grants, and package terms/sections/lessons in `GetPackagesQueryHandler` to determine package enrollment in-memory.

## Rationale
Using in-memory filtering avoids the N+1 query problem, as it queries all relations for all packages in a single batch (4 database calls total) instead of querying the database for each package inside the loop.

## Alternatives Considered
- Querying database per package: This would result in N+1 query pattern, which degrades performance as the number of packages grows.
- Adding database views: Overkill for this feature and complicates the C# schema definition.
