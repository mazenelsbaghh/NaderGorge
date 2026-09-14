# Contract: Performance and Observability

## Browser ingest

`POST /api/v1/metrics/web-vitals` (or the existing route kept as a compatible
alias) accepts a strict, bounded payload:

```json
{
  "metricId": "opaque-id",
  "metricName": "LCP",
  "value": 1834.2,
  "rating": "good",
  "routeTemplate": "/student/packages/[packageId]",
  "surface": "student",
  "deviceClass": "mobile",
  "connectionClass": "moderate",
  "navigationType": "client",
  "releaseId": "src-…",
  "correlationId": "optional-opaque-id"
}
```

### Rules

- Allowlisted metric names, ratings, surfaces, device/connection classes, and
  navigation types only.
- `routeTemplate` is normalized locally/server-side and never includes dynamic
  identifiers or query values.
- `releaseId`, metric ID, correlation ID, and all strings are length bounded.
- Reject non-finite/negative values and oversized payloads.
- Do not accept or persist raw full URL, query string, full user agent, token,
  cookie, name, phone, message, form value, or content text.
- Sampling is stable per anonymous browser session and its rate is
  configuration-visible.
- Public ingest, if enabled, uses a dedicated rate limit and returns no internal
  error detail.

## Summary read

An authorized operations/admin summary returns:

```json
{
  "filters": {
    "releaseId": "src-…",
    "routeTemplate": "/student",
    "surface": "student",
    "deviceClass": "mobile"
  },
  "sampleCount": 420,
  "segments": [
    {
      "metricName": "LCP",
      "p50": 1200,
      "p75": 1780,
      "p90": 2600,
      "p99": 6100,
      "goodRate": 0.82
    }
  ],
  "sampleQualified": false
}
```

- The response always reports `sampleCount`.
- `sampleQualified` is descriptive only; no fixed RUM count or duration blocks
  deployment.
- The endpoint is paginated/aggregated and authorized; it never exports
  identifiers or raw private sessions.

## Server correlation

- API responses propagate a safe `X-Correlation-Id`.
- Structured request evidence includes normalized route, method, status,
  duration, serving node, release ID, EF command count/time, and safe outcome.
- SQL text/parameters, authorization headers, cookies, bodies, support content,
  and secrets are excluded.
- Outbox metrics include claim wait, dispatch latency, retry/dead-letter count,
  event type, release ID, and node; payload content is excluded.

## Resource budgets

Version-controlled budgets distinguish:

- initial route JavaScript;
- shared JavaScript;
- deferred/async JavaScript;
- CSS;
- image/font transfer;
- request count and duplicate requests;
- click-to-usable navigation;
- API percentile and datastore command count.

Build budgets use effective compressed transfer size and map a resource to the
routes that require it. Existing raw all-chunk totals may remain diagnostic but
cannot be the only release gate.

## Blocking policy

Immediate synthetic browser, build-resource, API, query-count, authenticated
workflow, error-rate, health, and cluster gates block deployment.
Post-production RUM continues after rollout and is reported with sample size;
insufficient RUM is "pending/observational", never silently called successful.

## Required tests

- Payload schema, length/cardinality bounds, rate limit, and forbidden-data
  sentinel tests.
- Route normalization removes IDs and queries.
- Correlation is preserved browser → API → structured evidence.
- Route budget parser uses compressed sizes and fails an intentional breach.
- Live-support query-count ceiling holds for 1, 20, and 100 representative rows.
- Production smoke verifies cache headers, release/node identity, and RUM
  acceptance without secret leakage.
