# US1 Application Ingress Evidence

- Release: `src-0541078d8f68c5f05df6cf21f665e6714390d4e4`
- Full stack services running per node: 8
- Application image digest parity: verified on all three nodes
- Readiness: backend, database and Redis healthy from every node
- Distribution: 300 requests produced exactly 100 responses from each node
- Wrong Host: rejected with HTTP 421
- Failure drill: with node-3 gateway stopped, 120 requests completed as 60 on
  node-1 and 60 on node-2
- Current-release readiness drill: after draining node-3 on every ingress,
  60/60 requests completed without error as 30 on node-1 and 30 on node-2;
  node-3 then recovered healthy and `UP` everywhere
- Recovery: after node-3 returned, 30 requests distributed 10/10/10
- Final drift check found the application ingress section missing from the
  node-2 and node-3 HAProxy files. Both files were validated, synchronized and
  gracefully reloaded.
- Per-ingress verification: 60 requests through each node-local HAProxy
  distributed exactly 20/20/20 across node-1/node-2/node-3; invalid hosts
  returned HTTP 421 on every ingress.
- Public exposure: application and data ports closed; SSH remains key-only and
  rate-limited

Result: application ingress checkpoint passed before Cloudflare.
