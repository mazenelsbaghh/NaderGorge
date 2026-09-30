# Capability coverage

Current generated baseline checked on 2026-09-30:

- Items: 1,076 (597 backend-endpoint entries and 479 frontend-call entries)
- Candidate items: 456
- Blocked mutations/external effects: 620 (345 distinct authoritative-operation labels)
- Unresolved frontend calls: 0
- Reviewed non-business exclusions: 44 (13 AdminAI transport, 9 current-viewer playback, 1 auth refresh, 4 public or participant branches, 16 Teacher-only calls, 1 generic Learning Center dispatcher)
- Baseline digest: `36406d498a5ab43e359edb8ec7b9bb919cf6504b6785e91ba5f5efbddb0abc0b`
- Activation: `blocked`

The generated endpoint inventory has 831 backend endpoints and 712 frontend calls. Two Admin-accessible shared task mutation routes now map to their original commands, and both frontend calls inherit those exact backend labels. Sixteen Teacher-only branches retained by shared services are excluded after checking their Teacher role restrictions; Admin alternatives for reports, code groups, and financial statements were checked. Nine current-viewer playback calls reached by Admin lesson previews are excluded after checking the Admin preview policy and session ownership. The graph suite passed 11/11 and the inventory/source suite passed 30/30. The shared content-summary call is resolved to its proven Admin scope; the generic Learning Center read dispatcher is excluded as transport after all its concrete backend routes were inventoried. The Admin-accessible content reads, video-learning read, question-audio upload, and all 11 Learning Center routes now have backend inventory entries; public form, public settings, auth refresh, and participant-only branches have exact reviewed exclusions. This is not zero-gap coverage: unsupported current Admin mutations remain blocked, so the production catalog and feature activation must remain fail-closed.
