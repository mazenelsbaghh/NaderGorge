import fs from 'node:fs';
import path from 'node:path';

const frontendRoot = path.resolve(import.meta.dirname, '..');
const repositoryRoot = path.resolve(frontendRoot, '..');
const studentClient = fs.readFileSync(
  path.join(
    frontendRoot,
    'src/app/admin/students/AdminStudentsPageClient.tsx'
  ),
  'utf8'
);
const adminService = fs.readFileSync(
  path.join(frontendRoot, 'src/services/admin-service.ts'),
  'utf8'
);
const listUsersQuery = fs.readFileSync(
  path.join(
    repositoryRoot,
    'backend/src/NaderGorge.Application/Features/Admin/Queries/ListUsersQuery.cs'
  ),
  'utf8'
);

function assertContract(condition, message) {
  if (!condition) {
    throw new Error(`Admin student paging contract failed: ${message}`);
  }
}

assertContract(
  studentClient.includes('const STUDENT_PAGE_SIZES = [25, 50] as const'),
  'interactive page sizes must remain bounded to 25 or 50'
);
assertContract(
  studentClient.includes('}, 300);'),
  'student search must retain its 300ms debounce'
);
assertContract(
  studentClient.includes('new AbortController()') &&
    studentClient.includes('controller.abort()'),
  'superseded student requests must be cancelled'
);
assertContract(
  studentClient.includes("'Student',") &&
    studentClient.includes('pagination={false}'),
  'the server must own student filtering and pagination'
);
assertContract(
  studentClient.includes('adminService.exportUsers({') &&
    studentClient.includes("role: 'Student'"),
  'bulk export must use the dedicated paged export contract'
);
assertContract(
  adminService.includes('exportUsers: async (') &&
    adminService.includes('const pageSize = 100') &&
    adminService.includes('signal?: AbortSignal'),
  'bulk export must walk bounded cancellable pages'
);
assertContract(
  listUsersQuery.includes(
    'var pageSize = Math.Clamp(request.PageSize, 1, 100);'
  ) &&
    listUsersQuery.includes(
      'u.UserRoles.Any(ur => ur.Role.Name == normalizedRole)'
    ),
  'the application query must clamp page size and apply the role filter'
);

console.log('admin student paging contracts passed');
