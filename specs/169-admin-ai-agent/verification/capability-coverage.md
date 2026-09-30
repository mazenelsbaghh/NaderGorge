# Capability coverage

Current generated baseline checked on 2026-09-30:

- Items: 1,073 (579 backend-endpoint entries and 494 frontend-call entries)
- Candidate items: 450
- Blocked mutations/external effects: 623 (339 distinct authoritative-operation labels)
- Unresolved frontend calls: 23, including 11 mutations
- Reviewed non-business exclusions: 29 (13 AdminAI self-service transport, 16 Teacher-only calls)
- Baseline digest: `e05145fce26b401aa6b821c6322d3c88a7897662bf289291a0f1b5eb07b88757`
- Activation: `blocked`

The generated endpoint inventory has 831 backend endpoints and 712 frontend calls. Two Admin-accessible shared task mutation routes now map to their original commands, and both frontend calls inherit those exact backend labels. Sixteen Teacher-only branches retained by shared services are excluded after checking their Teacher role restrictions; Admin alternatives for reports, code groups, and financial statements were checked. The graph suite passed 10/10 and the inventory/source suite passed 26/26. This is not zero-gap coverage: unsupported current Admin mutations remain blocked, so the production catalog and feature activation must remain fail-closed.
