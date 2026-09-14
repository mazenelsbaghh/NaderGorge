# Capability coverage

Generated baseline on 2026-08-12:

- Items: 962
- Backend endpoints: 698
- Frontend calls: 585
- Candidate items: 400
- Blocked mutations/external effects: 562
- Baseline digest: `7e7bf6aca30c0e1a24486b55ad09226d9096fdb52f792ef1fa6e03688c5baebf`
- Activation: `blocked`

Inventory freshness and security tests pass. This is not zero-gap coverage: unsupported current Admin mutations remain blocked, so the production catalog and feature activation must remain fail-closed.
