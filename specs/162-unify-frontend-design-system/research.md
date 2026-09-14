# Research

## Decisions

- **Semantic token contract**: use a complete typed light/dark token set for canvas, surfaces, text, borders, focus, actions, and status pairs. **Rationale**: partial admin/student overrides currently leave contrast gaps. **Alternative rejected**: per-page `dark:` patches preserve drift.
- **Migration**: move by verified waves: foundations, primitives, public, student, teacher, assistant, admin, then live support. **Rationale**: preserves behavior and isolates regressions. **Alternative rejected**: global search/replace risks RTL, realtime, and visual regressions.
- **Color governance**: add a source scanner plus a narrow reviewed allowlist. **Rationale**: prevents new raw color drift. **Alternative rejected**: convention-only review is not enforceable.
- **Dialog strategy**: retain AccessibleDialog behavior and consolidate visual shells around it. **Rationale**: preserves focus trap, inert background, Escape, restore-focus, and reduced-motion behavior.
- **Live support**: styling-only migration, preserving websocket/realtime code and validating the existing client contracts. **Rationale**: live support has production routing constraints and must not change behavior.

## Validation Strategy

Run lint, typecheck, build, accessibility and design-token checks; role smoke/E2E in both themes; live-support and platform-event contracts; Docker config/up/health when environment is available.
