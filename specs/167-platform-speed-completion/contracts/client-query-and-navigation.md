# Contract: Client Query and Navigation

## Scope

This contract governs same-platform navigation, client-visible server data,
prefetch, realtime invalidation, and focus/scroll behavior across public,
student, parent, teacher, assistant, employee, admin, and live-support surfaces.

## Shell ownership

- Each surface layout owns exactly one persistent shell frame.
- Child pages, `loading.tsx`, and `error.tsx` render content regions only and
  must not instantiate another shell.
- Public navigation exists only inside the public route group.
- A surface shell may remount only after identity/surface change, logout, or a
  hard reload.
- Navigation visibility and route authorization consume the same typed policy;
  frontend visibility never replaces backend authorization.

## Canonical query keys

Keys are arrays with stable serializable segments:

```text
[surface, resource, scope, normalizedParameters]
```

Examples:

```text
['student', 'dashboard', userBoundary]
['student', 'packages', { page, pageSize, filter }]
['admin', 'students', { page, pageSize, search, sort }]
['support', 'conversation', conversationId]
```

- Parameters omit `undefined`, use deterministic field ordering, and normalize
  whitespace/case according to the server contract.
- Keys containing private data are scoped to the current authenticated identity
  boundary.
- Logout or user/role switch clears all protected cached queries before new
  content renders.

## Read lifecycle

- All HTTP reads call an existing/new function in `frontend/src/services`.
- Query functions accept and forward `AbortSignal` to Axios.
- One key may have at most one identical in-flight request.
- Superseded search/page requests are cancelled. A cancelled request emits no
  user error toast and cannot update cache.
- Lists retain prior successful data while a new page/filter loads, with an
  accessible regional busy state.
- Retry is bounded and disabled for authorization, validation, cancellation,
  and non-transient client failures.
- Freshness is selected by domain volatility; no browser cache is
  authorization authority.

## Mutation and realtime invalidation

- A mutation invalidates or patches only keys whose authoritative content
  changed.
- Realtime event mappings are explicit and testable. Unknown events do not
  trigger global refetch.
- Reconnect performs one authoritative reconciliation for affected domains and
  deduplicates stable event IDs.
- Dirty forms are never overwritten by background refetch.

## Pagination and search

- Large collection queries send a bounded page size; admin students defaults to
  25–50 and the server clamps its maximum.
- Search waits 250–350ms after input settles.
- Changing search/sort/filter resets the current page or cursor.
- Ordering is deterministic and includes a stable tie-breaker.
- Export is a separate bounded/streamed/queued contract; it must not request an
  unbounded interactive page.

## Navigation and prefetch

- Primary, safe, high-frequency routes may use framework prefetch.
- Heavy/rare routes use intent prefetch after pointer/focus/touch intent.
- Prefetch is disabled for unauthorized, external, destructive, save-data, or
  constrained-connection cases.
- Route prefetch may prefetch a matching safe read through its canonical key,
  but never performs mutations or caches another user's data.
- Same-origin navigation uses the App Router, not `location` or full reload.
- Authentication return destinations must be normalized same-origin paths and
  rejected if external or outside the user's authorized surface.

## Scroll and focus

- Each persistent shell exposes one `main` region and one skip link.
- Forward navigation focuses the destination heading or main region after
  content is ready without stealing focus from active input.
- Back/forward navigation restores the applicable shell content scroll
  position; new forward navigation starts at the contracted position.
- Dialogs/drawers contain focus, make the background inactive, close on Escape
  when safe, and restore the trigger.

## Required contract tests

- Shell identity is unchanged across two same-surface routes.
- No identical duplicate GET occurs in the contracted warm journey.
- Rapid search has at most one current request and no stale overwrite.
- Logout/user switch removes protected cache.
- Realtime event invalidates only mapped keys.
- Intent prefetch runs for eligible routes and not for denied/save-data routes.
- Deep-link return rejects external/open-redirect inputs.
- Keyboard focus, history scroll, and reduced-motion journeys pass.
