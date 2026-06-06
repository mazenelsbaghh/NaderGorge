# UI Contract: Deep Technical Audit Remediation

## QR Redemption Page

- Route: `/qr/[codeHash]`.
- States: loading auth, redirecting to login, redeeming, success redirect, recoverable error.
- Copy: Arabic-first, direct, no technical token/cookie language.
- Layout: mobile-safe centered panel, no horizontal scroll at 375px, 44px minimum touch targets.
- Accessibility: status text announced via normal visible content; retry/manual redemption link has focus state.

## Admin Worker Controls

- Components: AI monitor page and lesson video row controls.
- Data access: use `workerService`, not raw unauthenticated `fetch`.
- States: loading, unavailable, unauthorized, failed, canceling, retrying.
- Visibility: controls are available only when the current role is allowed.
- Styling: keep dense admin product UI, no decorative glass or new nested cards.

## Secure Video Player

- Parent and iframe use same-origin message validation.
- Commands use explicit target origin.
- Player state remains usable with keyboard-visible controls.
- Session consume happens after iframe load/ready, not before the iframe has a chance to render.
