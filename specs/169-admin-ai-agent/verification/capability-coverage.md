# Capability coverage

Current generated baseline checked on 2026-09-30:

- Items: 1,089 (579 backend-endpoint entries and 510 frontend-call entries)
- Candidate items: 461
- Blocked mutations/external effects: 628 (339 distinct authoritative-operation labels)
- Unresolved frontend calls: 39, including 16 mutations
- Baseline digest: `890be11ad9d7d3bf06bdc7a95a5059f3f4babf7e314f82c5b53b6c397675c797`
- Activation: `blocked`

The generated endpoint inventory has 831 backend endpoints and 712 frontend calls. Two Admin-accessible shared task mutation routes now map to their original commands, and both frontend calls inherit those exact backend labels. The graph suite passed 10/10 and the inventory/source suite passed 26/26. This is not zero-gap coverage: unsupported current Admin mutations remain blocked, so the production catalog and feature activation must remain fail-closed.
