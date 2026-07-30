import type { Pool } from 'pg';

export interface ClusterCronOptions {
  leaseName: string;
  ownerToken: string;
  leaseLifetimeMs: number;
  heartbeatIntervalMs?: number;
  delayUntilNextRun: () => number;
  task: (context: ClusterCronContext) => Promise<void>;
}

export interface ClusterCronContext {
  fencingGeneration: string;
  signal: AbortSignal;
}

export async function tryRunClusterCron(
  pool: Pool,
  options: ClusterCronOptions,
): Promise<boolean> {
  const claim = await pool.query<{ FencingGeneration: string }>(`
    INSERT INTO cluster_leases
      ("Name", "OwnerToken", "FencingGeneration", "ExpiresAt", "RenewedAt")
    VALUES
      ($1, $2, 1, NOW() + ($3 * INTERVAL '1 millisecond'), NOW())
    ON CONFLICT ("Name") DO UPDATE
    SET
      "OwnerToken" = EXCLUDED."OwnerToken",
      "FencingGeneration" = CASE
        WHEN cluster_leases."OwnerToken" = EXCLUDED."OwnerToken"
          THEN cluster_leases."FencingGeneration"
        ELSE cluster_leases."FencingGeneration" + 1
      END,
      "ExpiresAt" = EXCLUDED."ExpiresAt",
      "RenewedAt" = NOW()
    WHERE cluster_leases."ExpiresAt" <= NOW()
       OR cluster_leases."OwnerToken" = EXCLUDED."OwnerToken"
    RETURNING "FencingGeneration";
  `, [options.leaseName, options.ownerToken, options.leaseLifetimeMs]);
  if (claim.rowCount !== 1) {
    return false;
  }
  const leaseClaim = claim.rows[0];
  if (!leaseClaim) {
    throw new Error('Lease claim returned no fencing generation.');
  }
  const fencingGeneration = leaseClaim.FencingGeneration;
  const abortController = new AbortController();
  const heartbeatIntervalMs = options.heartbeatIntervalMs
    ?? Math.min(30_000, Math.max(1_000, Math.floor(options.leaseLifetimeMs / 3)));
  let heartbeatLost = false;
  let heartbeatBusy = false;
  const heartbeat = setInterval(async () => {
    if (heartbeatBusy || abortController.signal.aborted) {
      return;
    }
    heartbeatBusy = true;
    try {
      const renewed = await renewLease(pool, options, fencingGeneration);
      if (!renewed) {
        heartbeatLost = true;
        abortController.abort(new Error('Cluster lease fencing generation was lost.'));
      }
    } catch (error) {
      heartbeatLost = true;
      abortController.abort(error);
    } finally {
      heartbeatBusy = false;
    }
  }, heartbeatIntervalMs);
  heartbeat.unref();

  try {
    await options.task({ fencingGeneration, signal: abortController.signal });
    if (heartbeatLost || abortController.signal.aborted) {
      throw abortController.signal.reason ?? new Error('Cluster lease was lost.');
    }
    const completed = await recordOutcome(pool, options, fencingGeneration, 'completed');
    if (!completed) {
      throw new Error('Cluster lease was lost before the scheduled task completed.');
    }
    return true;
  } catch (error) {
    await recordOutcome(pool, options, fencingGeneration, 'failed');
    throw error;
  } finally {
    clearInterval(heartbeat);
  }
}

export function scheduleClusterCron(
  pool: Pool,
  options: ClusterCronOptions,
): void {
  const scheduleNext = () => {
    setTimeout(async () => {
      try {
        await tryRunClusterCron(pool, options);
      } catch (error) {
        console.error(`[cluster-cron] ${options.leaseName} failed`, error);
      } finally {
        scheduleNext();
      }
    }, options.delayUntilNextRun());
  };
  scheduleNext();
}

async function recordOutcome(
  pool: Pool,
  options: ClusterCronOptions,
  fencingGeneration: string,
  outcome: 'completed' | 'failed',
): Promise<boolean> {
  const result = await pool.query(`
    UPDATE cluster_leases
    SET "LastOutcome" = $4, "RenewedAt" = NOW()
    WHERE "Name" = $1
      AND "OwnerToken" = $2
      AND "FencingGeneration" = $3
      AND "ExpiresAt" > NOW();
  `, [options.leaseName, options.ownerToken, fencingGeneration, outcome]);
  return result.rowCount === 1;
}

async function renewLease(
  pool: Pool,
  options: ClusterCronOptions,
  fencingGeneration: string,
): Promise<boolean> {
  const result = await pool.query(`
    UPDATE cluster_leases
    SET
      "ExpiresAt" = NOW() + ($4 * INTERVAL '1 millisecond'),
      "RenewedAt" = NOW(),
      "LastOutcome" = 'running'
    WHERE "Name" = $1
      AND "OwnerToken" = $2
      AND "FencingGeneration" = $3
      AND "ExpiresAt" > NOW();
  `, [
    options.leaseName,
    options.ownerToken,
    fencingGeneration,
    options.leaseLifetimeMs,
  ]);
  return result.rowCount === 1;
}
