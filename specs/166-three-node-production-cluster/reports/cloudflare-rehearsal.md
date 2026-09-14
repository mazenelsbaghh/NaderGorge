# Cloudflare Rehearsal

Status: **ACTIVE AND HEALTHY**.

One Cloudflare Tunnel is installed with one connector on each Production node.
The corrected strict status probe requires either the token-based connector
unit or the legacy connector unit to be active before it accepts the metrics
endpoint. It passed for node-1, node-2 and node-3 after the final rolling
deployment:
`artifacts/production/cloudflare-status-20260729T190307Z/20260729T190308.761935Z-cloudflare-status.json`.
Every node reported `massar-cloudflared-token` active, readiness HTTP 200 and
four HA connections to Cloudflare.

The eight published routes are root, `app`, `admin`, `teacher`, `staff`, `api`,
`ws` and `assets`. Live HTTP checks observed the same immutable release through
all routed application hosts. WebSocket upgrade/reconnect passed through the
`ws` hostname, and a real uploaded file was read through the `assets` hostname
from every storage node.

Connector-loss continuity passed as a bounded one-replica service drill.
`massar-cloudflared-token` was stopped on node-3 only. The remaining two
connectors served 30/30 public/API requests with HTTP 200. The node-3 connector
was restored in `finally`, reached readiness in a total drill time of 27
seconds, and the post-check showed all three connectors healthy with four HA
connections each.
