# Data Model: Platform Verification Hygiene and Phase 1 Closure

This feature does not add database entities. It defines repository and verification-state concepts used by tasks and acceptance evidence.

## Verification Contract

- **Fields**:
  - `command`: exact shell command.
  - `scope`: backend, frontend, worker, Docker, E2E, hygiene, or deploy-safety.
  - `expected_result`: pass criteria or documented blocker.
  - `owner`: developer, CI, or credential owner.
- **Validation rules**:
  - Every command must be runnable from a documented working directory.
  - Missing scripts must have either a script addition or a documented substitute.

## Generated Artifact

- **Fields**:
  - `path_pattern`: e.g. `frontend/playwright-report/`, `frontend/test-results/`, `.next/`, mobile build/cache folders.
  - `source_control_state`: ignored, untracked, tracked, or intentionally tracked.
  - `action`: ignore, `git rm --cached`, leave tracked with reason.
- **Validation rules**:
  - Generated reports and build caches must not remain tracked unless explicitly allowlisted.
  - Source files and intentional docs must not be removed as artifacts.

## Secret Exposure Note

- **Fields**:
  - `location`: file and purpose where a secret or sensitive value was found.
  - `code_action`: removed, replaced with environment variable, or left with reason.
  - `rotation_required`: yes/no.
  - `rotation_owner`: external credential owner if not performable locally.
- **Validation rules**:
  - Removing a secret from code does not mark rotation complete.
  - Any previously committed secret requires a rotation note.

## E2E Surface Runtime

- **Fields**:
  - `frontend_origin`: app/admin/staff/teacher domain and port.
  - `api_origin`: backend domain and port.
  - `cookie_domain`: domain used by refresh cookies in E2E.
  - `cors_allowed_origins`: frontend origins accepted by backend.
- **State transitions**:
  - `misaligned` → `aligned` when frontend, backend, cookie domain, and CORS use the same local site strategy.
  - `aligned` → `verified` when Playwright browser smoke passes.
