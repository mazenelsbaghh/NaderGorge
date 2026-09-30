# Capability coverage

Current generated baseline checked on 2026-09-30:

- Items: 1,064 (579 backend-endpoint entries and 485 frontend-call entries)
- Candidate items: 449
- Blocked mutations/external effects: 615 (339 distinct authoritative-operation labels)
- Unresolved frontend calls: 14, including 3 mutations
- Reviewed non-business exclusions: 38 (13 AdminAI self-service transport, 9 current-viewer playback calls, 16 Teacher-only calls)
- Baseline digest: `80cc1170062cf7f1ab85bc22e1d81031ab55922fea9cf00c19e9c55f7acc871c`
- Activation: `blocked`

The generated endpoint inventory has 831 backend endpoints and 712 frontend calls. Two Admin-accessible shared task mutation routes now map to their original commands, and both frontend calls inherit those exact backend labels. Sixteen Teacher-only branches retained by shared services are excluded after checking their Teacher role restrictions; Admin alternatives for reports, code groups, and financial statements were checked. Nine current-viewer playback calls reached by Admin lesson previews are excluded after checking the Admin preview policy and session ownership. The graph suite passed 11/11 and the inventory/source suite passed 26/26. This is not zero-gap coverage: unsupported current Admin mutations remain blocked, so the production catalog and feature activation must remain fail-closed.
