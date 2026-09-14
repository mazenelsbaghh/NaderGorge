# Verification Quickstart

```bash
cd frontend
npm run lint
npm run typecheck
npm run build
npm run check:accessibility
npm run check:live-support-contracts
npm run check:platform-events
node scripts/check-query-contracts.mjs
node scripts/check-no-unallowlisted-reloads.mjs
```

After the implementation adds it, run `npm run check:design-tokens`. Run role E2E only with the configured backend, seed users, and browser runtime. Run `docker compose config -q`, `make up`, and `make ps`; migrations are not applicable because this feature changes no schema.
