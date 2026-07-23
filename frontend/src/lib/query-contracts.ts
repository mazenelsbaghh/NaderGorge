export type DataDomain =
  | 'users' | 'hr' | 'operations' | 'crm' | 'support' | 'content'
  | 'codes' | 'finance' | 'balance' | 'assessments' | 'community' | 'notifications'
  | 'media' | 'forms' | 'reports' | 'settings';

export type DataScope = DataDomain | 'subjects' | 'comments' | 'balance' | 'activity' | 'gamification' | 'ai' | 'watch-requests';
export type MutationOperation = 'create' | 'update' | 'delete' | 'bulk';

export type MutationContract = {
  owner: string;
  sourceFile: `frontend/src/services/${string}.ts`;
  mutationCount: number;
  endpointPattern: string;
  domain: DataDomain;
  keys: readonly string[];
  strategy: QueryContract['strategy'];
};

export type QueryContract = {
  owner: string;
  domain: DataDomain;
  scopes: readonly DataScope[];
  keys: readonly string[];
  operations: readonly MutationOperation[];
  strategy: 'response' | 'invalidate' | 'optimistic-rollback';
};

export const queryContracts: readonly QueryContract[] = [
  { owner: 'employee', domain: 'users', scopes: ['users', 'hr'], keys: ['employees', 'hr:employees'], operations: ['create', 'update', 'delete'], strategy: 'invalidate' },
  { owner: 'content', domain: 'content', scopes: ['content', 'subjects'], keys: ['content:packages', 'content:lessons'], operations: ['create', 'update', 'delete', 'bulk'], strategy: 'invalidate' },
  { owner: 'codes-finance', domain: 'codes', scopes: ['codes', 'finance', 'balance'], keys: ['codes:groups', 'finance:payroll', 'student:balance'], operations: ['create', 'update', 'delete', 'bulk'], strategy: 'invalidate' },
  { owner: 'assessment', domain: 'assessments', scopes: ['assessments', 'activity'], keys: ['student:exams', 'student:homeworks', 'assessments'], operations: ['create', 'update', 'delete'], strategy: 'invalidate' },
  { owner: 'community-notifications', domain: 'community', scopes: ['community', 'notifications', 'comments'], keys: ['community:posts', 'notifications', 'student:shell'], operations: ['create', 'update', 'delete'], strategy: 'invalidate' },
  { owner: 'operations-crm-support', domain: 'operations', scopes: ['operations', 'crm', 'support'], keys: ['operations:tasks', 'crm:queues', 'support:staff'], operations: ['create', 'update', 'delete', 'bulk'], strategy: 'invalidate' },
  { owner: 'forms-media-reports', domain: 'forms', scopes: ['forms', 'media', 'reports'], keys: ['forms', 'media', 'reports'], operations: ['create', 'update', 'delete', 'bulk'], strategy: 'invalidate' },
];

/**
 * Source-level mutation inventory. Counts are intentionally checked against
 * the service source by check-query-contracts.mjs so a new apiClient mutation
 * cannot silently bypass a refresh contract.
 */
type MutationContractSeed = readonly [string, number, DataDomain, readonly string[]];

export const mutationContracts: readonly MutationContractSeed[] = [
  ['admin-gifts-service.ts', 2, 'support', ['support:staff']],
  ['admin-sales-service.ts', 8, 'finance', ['finance:payroll', 'reports']],
  ['admin-service.ts', 89, 'users', ['employees', 'content:packages', 'assessments', 'student:balance']],
  ['advanced-report-service.ts', 5, 'reports', ['reports']],
  ['assistant-service.ts', 5, 'operations', ['operations:tasks', 'operations:dashboard']],
  ['auth-service.ts', 8, 'users', ['session']],
  ['balance-service.ts', 1, 'finance', ['student:balance', 'student:shell']],
  ['chat-service.ts', 4, 'support', ['support:staff']],
  ['code-service.ts', 2, 'codes', ['codes:groups', 'content:packages']],
  ['community-service.ts', 4, 'community', ['community:posts']],
  ['content-service.ts', 1, 'content', ['content:packages', 'content:lessons']],
  ['crm-service.ts', 2, 'crm', ['crm:queues', 'crm:calls', 'crm:reports']],
  ['exam-service.ts', 3, 'assessments', ['student:exams', 'assessments']],
  ['finance-service.ts', 8, 'finance', ['finance:payroll', 'finance:teacher', 'reports']],
  ['forms-service.ts', 5, 'forms', ['forms', 'reports']],
  ['homework-service.ts', 1, 'assessments', ['student:homeworks', 'assessments']],
  ['hr-governance-service.ts', 4, 'hr', ['hr:migration', 'hr:reports']],
  ['hr-payroll-service.ts', 5, 'hr', ['hr:payroll', 'hr:financial-requests']],
  ['hr-performance-service.ts', 5, 'hr', ['hr:performance', 'hr:cases']],
  ['hr-recruitment-service.ts', 6, 'hr', ['hr:recruitment', 'hr:lifecycle']],
  ['hr-service.ts', 23, 'hr', ['hr:employees', 'hr:organization', 'hr:contracts', 'hr:shifts', 'hr:attendance', 'hr:corrections', 'hr:leave', 'hr:approvals', 'hr:documents', 'hr:assets']],
  ['live-support-ai-service.ts', 7, 'support', ['support:ai', 'support:dashboard']],
  ['live-support-service.ts', 23, 'support', ['support:staff', 'support:dashboard', 'support:ai']],
  ['media-service.ts', 3, 'media', ['media', 'reports']],
  ['recharge-service.ts', 2, 'finance', ['student:balance', 'reports']],
  ['report-service.ts', 1, 'reports', ['reports']],
  ['shared-package-service.ts', 4, 'content', ['content:packages', 'student:shell']],
  ['student-service.ts', 6, 'community', ['community:posts', 'notifications', 'student:shell']],
  ['teacher-service.ts', 20, 'content', ['content:packages', 'assessments', 'community:posts']],
  ['video-session-service.ts', 4, 'content', ['content:lessons', 'watch-requests']],
  ['wallet-service.ts', 5, 'finance', ['finance:payroll', 'student:balance']],
  ['whatsapp-service.ts', 1, 'users', ['users']],
];

export const mutationContractRecords: readonly MutationContract[] = mutationContracts.map(
  ([sourceFile, mutationCount, domain, keys]) => ({
    owner: sourceFile.replace(/-service\.ts$/, ''),
    sourceFile: `frontend/src/services/${sourceFile}` as MutationContract['sourceFile'],
    mutationCount,
    endpointPattern: 'apiClient.(post|put|patch|delete)',
    domain: domain as DataDomain,
    keys,
    strategy: 'invalidate',
  }),
);

export function validateQueryContracts(contracts: readonly QueryContract[] = queryContracts): string[] {
  const errors: string[] = [];
  for (const [index, contract] of contracts.entries()) {
    if (!contract.owner || !contract.domain || contract.scopes.length === 0 || contract.keys.length === 0 || contract.operations.length === 0) {
      errors.push(`contract[${index}] is missing owner, domain, scopes, keys, or operations`);
    }
  }
  const sourceFiles = new Set(mutationContractRecords.map((contract) => contract.sourceFile));
  if (sourceFiles.size !== mutationContractRecords.length) errors.push('mutation contract source files must be unique');
  return errors;
}
