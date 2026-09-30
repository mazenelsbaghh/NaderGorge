# Capability coverage

Current generated baseline checked on 2026-09-29:

- Items: 1,125 (505 backend-endpoint entries and 620 frontend-call entries)
- Candidate items: 466
- Blocked mutations/external effects: 659 (307 distinct authoritative-operation labels; 94 direct-controller write extraction blockers)
- Baseline digest: `4de64937bd7cd641b87d00b416df68546b2b1e7f9d1fa063fc212a7bff524435`
- Activation: `blocked`

Inventory freshness and security tests pass. This is not zero-gap coverage: unsupported current Admin mutations remain blocked, so the production catalog and feature activation must remain fail-closed.
