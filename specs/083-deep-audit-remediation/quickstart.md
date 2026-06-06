# Quickstart: Deep Technical Audit Remediation

## Verification Commands

```bash
dotnet build backend/NaderGorge.sln
dotnet test backend/NaderGorge.sln --no-build
cd frontend && npm run lint && npm run build
cd ../worker && npm run build
cd ..
python3 -m pip install -r tests/requirements.txt
python3 -m pytest tests/test_endpoint_inventory.py tests/test_codes.py tests/test_purchases.py tests/test_video.py -q
node scripts/generate-endpoint-inventory.mjs --check
docker compose config -q
```

## Manual Smoke Scenarios

1. Log in as Admin or Teacher, open `/admin/ai-monitor`, verify job status requests do not return proxy 401 and cancel/retry requires staff role.
2. Log in as Student, open `/api/qr/{validCode}`, verify it redirects to `/qr/{validCode}` and redeems using the active session.
3. Open the student homework flow and verify pending/submit calls use `/api/homework/*`.
4. Run concurrent code redemption and purchase tests, verify only one state change succeeds.
5. Open a secure video, inspect iframe URL, verify it contains only a session id and no `t=`/`k=` token/key material.
6. Send a forged `postMessage` from a different origin in browser devtools or automated test context, verify player ignores it.
7. Regenerate endpoint inventory and confirm internal/E2E routes are not classified as plain anonymous.
