import { mutationContractRecords } from './query-contracts';

type MutationFailure = 'validation' | 'network' | 'cancelled' | 'permission';

function classifyMutationFailure(error: { response?: { status?: number }; code?: string }): MutationFailure {
  if (error.code === 'ERR_CANCELED' || error.code === 'ECONNABORTED') return 'cancelled';
  if (!error.response) return 'network';
  if (error.response.status === 400 || error.response.status === 422) return 'validation';
  if (error.response.status === 401 || error.response.status === 403) return 'permission';
  return 'network';
}

const cases: Array<[Parameters<typeof classifyMutationFailure>[0], MutationFailure]> = [
  [{ response: { status: 422 } }, 'validation'],
  [{}, 'network'],
  [{ code: 'ERR_CANCELED' }, 'cancelled'],
  [{ response: { status: 403 } }, 'permission'],
];

for (const [error, expected] of cases) {
  if (classifyMutationFailure(error) !== expected) {
    throw new Error(`Mutation failure contract failed for ${expected}.`);
  }
}

if (mutationContractRecords.some((contract) => contract.strategy === 'optimistic-rollback')) {
  throw new Error('No service mutation may opt into optimistic state without an explicit rollback test.');
}
