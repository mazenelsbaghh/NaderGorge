# Cloudflare Cutover

## Live routes

The final Cloudflare Tunnel routes are active for:

- `massar-academy.net`
- `app.massar-academy.net`
- `admin.massar-academy.net`
- `teacher.massar-academy.net`
- `staff.massar-academy.net`
- `api.massar-academy.net`
- `ws.massar-academy.net`
- `assets.massar-academy.net`

All routes are proxied by Cloudflare and terminate at the three-replica Tunnel.
The six page/API roots return HTTP 200 on release
`src-bdf19804cf29d19634b131a16d3e519d26f0d425`; `ws` and `assets` correctly
return HTTP 404 at `/` while their real hub and asset paths pass.

The application gateway accepts only the reviewed host allowlist. Internal
application, PostgreSQL, Redis, etcd, Gluster and backup endpoints are not
published to the Internet. Cloudflare status, cross-node WebSocket and real
shared-file evidence are linked from `cloudflare-rehearsal.md` and
`manual-qa.md`.

An external TCP check confirmed that only SSH/22 is visible on each public
server address. The direct HTTP origins and the application/data ports are
closed, so browser traffic must traverse Cloudflare Tunnel.

Final capacity acceptance remains conditional on the provider CPU-steal gate;
DNS and Tunnel routing themselves are complete.
