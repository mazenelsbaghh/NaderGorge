# Cloudflare Cutover for `massar-academy.net`

## Target design

Use one locally managed Cloudflare Tunnel and run the same tunnel as three
connector replicas, one on each production server. The reviewed installer uses
the tunnel UUID plus the credentials JSON created by `cloudflared tunnel
create`; it does not accept a remotely managed connector token. Every
connector sends traffic to `http://127.0.0.1:8088`; the node-local HAProxy then
balances the request over all three ready application nodes.

This does not require Cloudflare Load Balancing. Do not publish origin `A` or
`AAAA` records and do not expose ports 80 or 443 on the servers. Cloudflare's
proxied edge and Tunnel hide the origin addresses and provide the standard
Cloudflare DDoS layer.

## Host routing

Create exactly these public hostnames on the same tunnel:

| Public hostname | Local service |
|---|---|
| `massar-academy.net` | `http://127.0.0.1:8088` |
| `app.massar-academy.net` | `http://127.0.0.1:8088` |
| `admin.massar-academy.net` | `http://127.0.0.1:8088` |
| `teacher.massar-academy.net` | `http://127.0.0.1:8088` |
| `staff.massar-academy.net` | `http://127.0.0.1:8088` |
| `api.massar-academy.net` | `http://127.0.0.1:8088` |
| `ws.massar-academy.net` | `http://127.0.0.1:8088` |
| `assets.massar-academy.net` | `http://127.0.0.1:8088` |

The repository template is
`deploy/production/config/cloudflared/config.yml.tmpl`. Its catch-all rule
returns 404 and must stay last.

## Owner actions in Cloudflare

1. Add `massar-academy.net` to Cloudflare and review the imported DNS records.
2. Replace the registrar nameservers with the two nameservers Cloudflare assigns
   to the zone. Do not change application records yet.
3. On the protected operator workstation, authenticate `cloudflared`, then
   create one locally managed production tunnel. Keep its UUID and generated
   credentials JSON outside the repository with mode `0600`.
4. Pass the UUID and credentials-file path to the reviewed
   `manage_cloudflare.py install` workflow. Never paste the JSON into Git, chat
   logs, shell history or an environment example.
5. Install the same tunnel credentials and rendered local config as a system
   service on all three nodes.
6. Confirm three healthy connector replicas before adding public hostnames.
7. Rehearse first with an Access-protected temporary hostname and a Tunnel DNS
   route. The pre-DNS cluster acceptance does not require this external drill;
   the rehearsal starts only after that decision is `GO`.
8. After the rehearsal passes, create Tunnel DNS routes for the eight hostnames,
   remove the temporary rehearsal route/config, and remove conflicting old
   origin records.

The rehearsal install command accepts `--tunnel-id`, `--credentials` and
`--rehearsal-hostname rehearsal.massar-academy.net`. It adds that hostname
before the deny fallback and overrides the origin Host header to the approved
landing host. Protect the rehearsal hostname with Cloudflare Access before
creating its DNS route. Re-run the installer without `--rehearsal-hostname`
after the rehearsal so the final connector configuration contains only the
eight approved hosts plus the deny fallback.

## Zone controls

- Enable Always Use HTTPS and TLS 1.2 or newer.
- Use Full (strict) for any direct HTTPS origin that may be added later. Tunnel
  traffic in this design terminates locally at HAProxy over loopback.
- Enable the managed WAF rules available on the selected Cloudflare plan.
- Add rate limits for login, password reset, OTP, upload and expensive API
  endpoints. Start in logging mode, then tune before blocking.
- Keep WebSockets enabled for `ws.massar-academy.net`.
- Bypass cache for `api`, `ws`, authenticated responses, cookies and protected
  assets.
- Cache only versioned public assets on `assets.massar-academy.net`.
- Allow credentialed CORS only from the five approved browser surfaces; never
  combine credentials with a wildcard origin.
- Use secure, HTTP-only authentication cookies with an explicitly reviewed
  domain and SameSite policy.

## Cutover gates

Do not start the protected rehearsal until the signed pre-DNS decision is `GO`.
That decision requires:

- all three app nodes on identical image digests;
- application, PostgreSQL, Redis and Gluster health green;
- internal three-node database and file backups current;
- isolated database and file restore tests passed;
- Admin account created through the no-echo bootstrap;
- pre-DNS HTTP/API/WebSocket/upload/protected-asset checks passed;
- wrong-host requests and all direct origin/data ports denied;
- bounded ingress/app/PostgreSQL/Redis/files/worker failure drills passed.

The final Tunnel DNS routes remain blocked until the rehearsal proves three
healthy connector replicas, one-connector loss, and the browser/API/WebSocket/
cookie/upload/protected-asset paths. After cutover, test all eight hosts from
outside the server network, then stop one connector and one app gateway
separately. Traffic must continue through the remaining nodes. Keep the
previous DNS values recorded for rollback, but do not restore public origin
exposure.
