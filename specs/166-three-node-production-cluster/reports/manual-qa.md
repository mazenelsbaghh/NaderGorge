# Manual QA Status

Last live pass: 2026-07-29

Running release: `src-bdf19804cf29d19634b131a16d3e519d26f0d425`

## Passed before domain cutover

- All required host services and the shared mount are active on all three
  nodes.
- Every node-local ingress returns healthy routed API and landing readiness.
- The same immutable running release is visible from all three ingresses.
- Distribution and one-node application/ingress loss drills passed.
- Wrong Host is denied, and application/data ports are not publicly exposed.
- PostgreSQL writer failover, Redis master failover and Gluster heal/split-brain
  status passed for the running release.
- The initial Production Admin login returned the `Admin` role, and the same
  token authorized the protected roles endpoint directly on node-1, node-2
  and node-3.
- Disposable Student, Teacher and Staff users were created through the
  protected Admin API. Each logged in successfully only through its intended
  application surface and returned exactly its assigned role. The Teacher also
  had a real Teacher profile. The evidence contains no credentials:
  `artifacts/production/manual-qa/role-login-5350639.json`.
- The same disposable accounts were then exercised through the real browser
  forms. Student reached `/student` and its empty-state dashboard, Teacher
  reached `/teacher` and the owned-content dashboard, and Staff reached
  `/assistant/dashboard` with only the permitted staff navigation. The Staff
  account intentionally had no Employee profile, so its attendance widget
  showed the expected profile-not-provisioned state without blocking the rest
  of the dashboard.
- The public landing page and the student, Admin, teacher, staff and Thanaweya
  result pages were opened in a real Chrome session. Titles and primary
  navigation rendered, the public page had no broken images, and the logged-in
  Admin dashboard exposed 18 authorized sections.
- A valid PNG was uploaded through the real protected popup-image endpoint,
  converted to WebP, read byte-identically through the `assets` gateway on
  node-1, node-2 and node-3, and then deleted. Its exact shared Gluster path was
  absent on all three nodes after cleanup.
- An authenticated SignalR client upgraded on node-2, lost a backend, then
  reconnected and completed a second handshake on node-3. Both HTTP 101 checks
  passed. See
  `artifacts/production/signalr-live-20260729/reconnect-proven-complete-20260729T183045Z.log`.
- The Admin browser was reopened after the final rolling deployment. The
  previous SignalR start/stop race did not recur, and there were no broken
  images. Two console messages came from a Chrome extension message channel,
  not the application or SignalR.
- All eight final hostnames resolve through Cloudflare Tunnel and expose the
  same immutable release. Root, app, admin, teacher, staff and API returned
  HTTP 200. The roots of `ws` and `assets` returned the expected HTTP 404
  because those hosts expose endpoint paths rather than web pages. A 90-request
  API sample reached all three application nodes.
- Negative authorization checks passed: Student and Teacher tokens received
  HTTP 403 from the protected Admin user endpoint, and the Staff token received
  HTTP 403 from a missing `content.manage` endpoint. Anonymous `/protected/`
  and `/private/` asset probes returned HTTP 404.
- An external TCP probe against all three public IPs found only SSH/22 open.
  Ports 80, 443, 5432, 6379, 2379, 2380, 8080, 8088, 9000 and 9241 were closed,
  so the origin and all data/application ports remain unavailable outside the
  Tunnel.
- A bounded node-3 Worker loss left node-1 and node-2 Workers ready and
  preserved 30/30 API requests. The exact stopped Worker container was
  restarted in `finally` and returned ready; total drill time was 16.29 seconds.
- A bounded node-3 Tunnel connector loss preserved 30/30 public/API requests
  through the remaining two replicas. The connector recovered within the
  60-second bound and all three replicas returned to four HA connections.

## Remaining provider-dependent check

The only remaining infrastructure acceptance issue is VPS CPU steal. The
application and data-path manual checks are green; capacity remains NO-GO if a
fresh 30-minute resource sample exceeds the 5% steal threshold.
