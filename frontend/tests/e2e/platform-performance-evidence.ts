import { createHash } from 'node:crypto';

export type EligibleReadCategory = 'api-read' | 'rsc-read';

type RequestIdentityInput = {
  method: string;
  resourceType: string;
  url: string;
};

export type EligibleReadOrigins = {
  appOrigin: string;
  apiOrigin: string;
};

export type EligibleReadIdentity = {
  identitySha256: string;
  category: EligibleReadCategory;
};

export type EligibleReadCount = EligibleReadIdentity & {
  count: number;
};

export function eligibleReadIdentity(
  request: RequestIdentityInput,
  allowedOrigins: EligibleReadOrigins,
): EligibleReadIdentity | null {
  if (
    request.method !== 'GET' ||
    (request.resourceType !== 'fetch' && request.resourceType !== 'xhr')
  ) {
    return null;
  }

  const url = new URL(request.url);
  const appOrigin = new URL(allowedOrigins.appOrigin).origin;
  const apiOrigin = new URL(allowedOrigins.apiOrigin).origin;
  const originClass = url.origin === appOrigin
    ? 'app'
    : url.origin === apiOrigin
      ? 'api'
      : null;
  if (!originClass || /\/metrics\/web-vitals\/?$/.test(url.pathname)) return null;

  const category: EligibleReadCategory = url.searchParams.has('_rsc')
    ? 'rsc-read'
    : 'api-read';
  url.searchParams.delete('_rsc');
  const sortedQuery = [...url.searchParams.entries()].sort(
    ([leftKey, leftValue], [rightKey, rightValue]) =>
      leftKey.localeCompare(rightKey) || leftValue.localeCompare(rightValue),
  );
  const canonicalQuery = new URLSearchParams(sortedQuery).toString();
  const identitySha256 = createHash('sha256')
    .update(`${category}\0${originClass}\0${url.pathname}\0${canonicalQuery}`)
    .digest('hex');

  return { identitySha256, category };
}

export function aggregateEligibleReads(
  identities: EligibleReadIdentity[],
): EligibleReadCount[] {
  const counts = new Map<string, EligibleReadCount>();
  for (const identity of identities) {
    const key = `${identity.category}:${identity.identitySha256}`;
    const current = counts.get(key);
    if (current) {
      current.count += 1;
    } else {
      counts.set(key, { ...identity, count: 1 });
    }
  }

  return [...counts.values()].sort((left, right) =>
    `${left.category}:${left.identitySha256}`.localeCompare(
      `${right.category}:${right.identitySha256}`,
    ),
  );
}

export function nearestRankP75(values: number[], expectedCount = 20) {
  if (
    values.length !== expectedCount ||
    values.some((value) => !Number.isFinite(value) || value < 0)
  ) {
    throw new Error(`Nearest-rank p75 requires ${expectedCount} finite non-negative samples.`);
  }

  const sorted = [...values].sort((left, right) => left - right);
  return sorted[Math.ceil(0.75 * sorted.length) - 1]!;
}
