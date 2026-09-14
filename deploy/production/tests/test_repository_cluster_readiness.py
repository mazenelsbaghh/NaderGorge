from __future__ import annotations

import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]


def production_sources() -> list[Path]:
    roots = (
        ROOT / "backend/src",
        ROOT / "worker/src",
        ROOT / "deploy/production/scripts",
    )
    return [
        path
        for root in roots
        for path in root.rglob("*")
        if path.is_file()
        and path.suffix in {".cs", ".ts", ".py", ".sh"}
        and ".test." not in path.name
        and "__pycache__" not in path.parts
    ]


def test_only_shared_storage_implementations_write_durable_application_files() -> None:
    allowed = {
        ROOT / "backend/src/NaderGorge.Infrastructure/Services/SharedFileStorage.cs",
        ROOT / "worker/src/config/storage.ts",
    }
    pattern = re.compile(
        r"(?:File\.Write|File\.Create\(|new FileStream\([^\n]*FileMode\.Create|writeFileSync\()"
    )
    findings = [
        str(path.relative_to(ROOT))
        for path in production_sources()
        if path not in allowed and pattern.search(path.read_text(encoding="utf-8"))
    ]
    assert findings == []


def test_production_clients_fail_closed_without_cluster_database_or_sentinel() -> None:
    database = (ROOT / "worker/src/config/database.ts").read_text(encoding="utf-8")
    redis = (ROOT / "worker/src/config/redis.ts").read_text(encoding="utf-8")
    dotnet_redis = (
        ROOT / "backend/src/NaderGorge.Infrastructure/Cache/RedisConnectionFactory.cs"
    ).read_text(encoding="utf-8")
    assert "NODE_ENV === 'production'" in database
    assert "DATABASE_URL is required in production" in database
    assert "NODE_ENV === 'production'" in redis
    assert "Sentinel configuration is required in production" in redis
    assert 'config["ASPNETCORE_ENVIRONMENT"]' in dotnet_redis
    assert "Redis Sentinel configuration is required in production" in dotnet_redis


def test_every_singleton_scheduler_has_a_database_lease_or_atomic_claim() -> None:
    required = {
        "RechargeRequestExpiryBackgroundService.cs",
        "LiveSupportRecoveryBackgroundService.cs",
        "LiveSupportAIRecoveryBackgroundService.cs",
        "HrApprovalEscalationService.cs",
    }
    for name in required:
        path = next((ROOT / "backend/src/NaderGorge.API").rglob(name))
        assert "ClusterLeaseRunner.TryRunAsync" in path.read_text(encoding="utf-8")
    worker = (ROOT / "worker/src/index.ts").read_text(encoding="utf-8")
    assert "scheduleClusterCron" in worker
    assert "runNightlySweep" in worker


def test_operational_scripts_do_not_target_public_nodes_with_literals() -> None:
    public_addresses = ("191.218.161.76", "191.218.161.78", "168.231.106.230")
    findings = []
    for path in (ROOT / "deploy/production/scripts").glob("*"):
        if not path.is_file():
            continue
        text = path.read_text(encoding="utf-8", errors="ignore")
        if any(address in text for address in public_addresses):
            findings.append(path.name)
    assert findings == []


def test_outbox_and_bullmq_retries_use_stable_ids() -> None:
    outbox = (
        ROOT / "backend/src/NaderGorge.API/BackgroundServices/OutboxProcessorBackgroundService.cs"
    ).read_text(encoding="utf-8")
    lease_store = (
        ROOT / "backend/src/NaderGorge.Infrastructure/Background/OutboxLeaseStore.cs"
    ).read_text(encoding="utf-8")
    enqueuer = (
        ROOT / "backend/src/NaderGorge.Infrastructure/Background/RedisJobEnqueuer.cs"
    ).read_text(encoding="utf-8")
    worker = (ROOT / "worker/src/queues/jobIngestion.ts").read_text(encoding="utf-8")
    assert "CreateLeaseStore" in outbox
    assert "FOR UPDATE SKIP LOCKED" in lease_store
    assert "TryAcknowledgeAsync" in lease_store
    assert "outboxEventId = value.Id" in outbox
    assert "ResolveStableJobId" in enqueuer
    assert "getJob(targetJobId)" in worker
