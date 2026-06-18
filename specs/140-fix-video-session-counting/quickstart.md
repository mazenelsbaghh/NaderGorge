# Quickstart: Session-Safe Video View Counting

## Preconditions

- Docker services and test data are available.
- A student has access to a lesson video with a known duration, threshold percentage, and maximum view count of at least two.
- Apply the new EF migration before runtime checks.

## Automated verification

```bash
dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter VideoWatchProgressTests
dotnet test backend/NaderGorge.sln --no-restore
npm --prefix frontend run lint
npm --prefix frontend run build
npm --prefix frontend run test:e2e -- video-session-counting.spec.ts
python3 -m pytest tests/test_video.py -k watch -q
docker compose config -q
```

## Manual verification

1. Sign in as a student and open an accessible lesson video.
2. Reach the configured threshold, note the count, then continue playback, seek forward/backward, and wait through multiple sync intervals. The count and tracked remainder must not advance toward another view.
3. Watch less than a threshold in a fresh/reset case, refresh, and continue. The accepted seconds from both sessions must combine.
4. After one session registers a view, refresh and watch to the threshold. Exactly one additional view may register from the new session.
5. Open the same video in a second tab. The first tab must pause after its next progress response and display the newer-tab/device explanation and reload action. Its request must not change persisted watch state.
6. Keep one session actively playing longer than the original five-minute expiry. Regular valid progress must renew it without granting a second view.
7. Repeat maximum locking, approved extra view, admin reset, and lesson repurchase flows and confirm existing results.

## Docker closure

```bash
make up
make migrate
make ps
curl -f http://localhost:5245/api/health
curl -f http://localhost:8738
curl -f http://localhost:3001/ui
```

Record any unavailable environment-dependent check explicitly; do not treat build/lint alone as behavioral verification.
