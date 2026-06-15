import { execSync } from 'child_process';
import path from 'path';

// Blocked patterns/extensions
const BLOCKED_EXTENSIONS = ['.bak', '.dump', '.db', '.tar', '.gz', '.zip'];
const BLOCKED_FILENAMES = ['.env', '.env.production', '.env.development', '.env.staging', '.env.test'];

// Safe schema files that are allowed to be tracked
const ALLOWED_SQL_FILES = [
  'backend/src/nadergorge.api/script.sql',
  'docker/apply_pending_migrations.sql',
  'docker/create_missing_tables.sql'
];

try {
  const stdout = execSync('git ls-files', { encoding: 'utf8' });
  const files = stdout.split('\n').map(f => f.trim()).filter(Boolean);

  let violations = [];

  for (const file of files) {
    const lowerFile = file.toLowerCase();
    const ext = path.extname(lowerFile);
    const basename = path.basename(lowerFile);

    // Check blocked extensions
    if (ext === '.sql') {
      if (!ALLOWED_SQL_FILES.includes(lowerFile)) {
        violations.push({ file, reason: 'Tracked SQL dump file is not in the allowed list.' });
      }
    } else if (BLOCKED_EXTENSIONS.includes(ext)) {
      violations.push({ file, reason: `Blocked extension "${ext}".` });
    }

    // Check blocked filenames (like .env variations)
    if (BLOCKED_FILENAMES.includes(basename) || (basename.startsWith('.env') && basename !== '.env.example')) {
      violations.push({ file, reason: 'Tracked environment or secret configuration file.' });
    }
  }

  if (violations.length > 0) {
    console.error('❌ SECURITY ERROR: Sensitive or database backup files are tracked in git:');
    for (const v of violations) {
      console.error(`  - ${v.file}: ${v.reason}`);
    }
    process.exit(1);
  }

  console.log('✅ Security scan passed: No sensitive tracked files found.');
  process.exit(0);
} catch (error) {
  console.error('Failed to run git verification scan:', error);
  process.exit(1);
}
