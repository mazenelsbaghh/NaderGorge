# Capability coverage

Current generated baseline checked on 2026-09-30:

- Items: 1,080 (579 backend-endpoint entries and 501 frontend-call entries)
- Candidate items: 457
- Blocked mutations/external effects: 623 (339 distinct authoritative-operation labels)
- Unresolved frontend calls: 30, including 11 mutations
- Reviewed non-business exclusions: 22 (13 AdminAI self-service transport, 9 Teacher-only report calls)
- Baseline digest: `ea7b19ead4582cdf5e07a3aa2fbe332e0a20caad130b771d63c1e009ef7bc999`
- Activation: `blocked`

The generated endpoint inventory has 831 backend endpoints and 712 frontend calls. Two Admin-accessible shared task mutation routes now map to their original commands, and both frontend calls inherit those exact backend labels. Nine Teacher-only report branches in the shared audience service are excluded only after checking the Teacher role restriction and matching Admin report paths. The graph suite passed 10/10 and the inventory/source suite passed 26/26. This is not zero-gap coverage: unsupported current Admin mutations remain blocked, so the production catalog and feature activation must remain fail-closed.
