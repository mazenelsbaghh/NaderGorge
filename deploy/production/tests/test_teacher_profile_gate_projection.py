"""Regression: a nullable teacher column must not look like changed old values."""
import importlib.util
from contextlib import contextmanager
import os
from pathlib import Path
import shlex
import shutil
import subprocess
import tempfile
import unittest
import uuid


SCRIPT = Path(__file__).resolve().parents[1] / 'scripts/prepare_release_migration_gate.py'


@contextmanager
def isolated_postgres():
    initdb = shutil.which('initdb')
    if not initdb:
        raise unittest.SkipTest('PostgreSQL binaries are required for this isolated integration test')
    binaries = Path(initdb).resolve().parent
    temporary = tempfile.TemporaryDirectory(prefix='massar-gate-pg-')
    root = Path(temporary.name)
    sockets = root / 'sockets'
    sockets.mkdir()
    cluster = root / 'data'
    subprocess.run([str(binaries / 'initdb'), '-D', str(cluster), '-A', 'trust', '-U', 'gate', '--no-locale'], check=True, capture_output=True)
    ctl = [str(binaries / 'pg_ctl'), '-D', str(cluster)]
    options = shlex.join(['-h', '', '-k', str(sockets), '-p', '6543', '-F'])
    subprocess.run([*ctl, '-l', str(root / 'postgres.log'), '-o', options, '-w', 'start'], check=True, capture_output=True)
    try:
        yield {**os.environ, 'PGHOST': str(sockets), 'PGPORT': '6543', 'PGUSER': 'gate', 'PGDATABASE': 'postgres', 'PATH': f'{binaries}:{os.environ["PATH"]}'}
    finally:
        subprocess.run([*ctl, '-m', 'immediate', '-w', 'stop'], check=True, capture_output=True)
        temporary.cleanup()


def verify_nullable_column(postgres, existing_columns):
    spec = importlib.util.spec_from_file_location('teacher_projection_gate', SCRIPT)
    runner = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(runner)
    rendered = runner.remote_producer_script(operation_id='gate-' + '0' * 32, release_id='git-' + '0' * 40,
        manifest_sha256='0' * 64, migrator_digest='sha256:' + '0' * 64)
    protected_hash = 'protected_rows_hash() {' + rendered.split('protected_rows_hash() {', 1)[1].split('\n}\n', 1)[0] + '\n}\n'
    setup = rendered.split('pre_finance_preset_exists=', 1)[1].split('\nfinance_activation_count()', 1)[0]
    setup = 'pre_finance_preset_exists=' + setup
    validation = rendered.split('test "$(protected_rows_hash)" = "$pre_protected_rows_hash"\n', 1)[1].split("psql_restore -c 'DROP SCHEMA massar_gate_refund_roles", 1)[0]
    column_sql = ''.join(f', "{column}" {"integer" if column == "FinancePreset" else "text"}' for column in existing_columns)
    schema = f'''
CREATE TABLE users ("Id" uuid);
CREATE TABLE user_roles ("UserId" uuid, "RoleId" uuid);
CREATE TABLE teacher_profiles ("Id" uuid, "Bio" text{column_sql});
INSERT INTO teacher_profiles ("Id", "Bio") VALUES ('00000000-0000-0000-0000-000000000001', 'original');
CREATE TABLE teacher_subjects ("TeacherId" uuid, "SubjectId" uuid);
CREATE TABLE subjects ("Id" uuid);
CREATE TABLE roles ("Id" uuid, "Name" text);
CREATE SCHEMA massar_gate_refund_roles;
CREATE TABLE massar_gate_refund_roles.roles ("Id" uuid);
CREATE TABLE "__EFMigrationsHistory" ("MigrationId" text);
'''
    database = 'gate_' + uuid.uuid4().hex
    subprocess.run(['createdb', database], env=postgres, check=True, capture_output=True)
    env = {**postgres, 'PGDATABASE': database}
    try:
        subprocess.run(['psql', '-Xq', '-v', 'ON_ERROR_STOP=1'], input=schema, text=True, env=env, check=True, capture_output=True)
        script = '''set -euo pipefail
psql_restore() { psql -XAt -v ON_ERROR_STOP=1 "$@"; }
''' + protected_hash + setup + '\nvalidate_new_column() {\n' + validation + '\n}\n' + r'''
before="$(protected_rows_hash)"
validate_new_column
if test "$pre_teacher_whatsapp_exists" = 0; then
  psql_restore -c 'ALTER TABLE teacher_profiles ADD COLUMN "WhatsAppUrl" text;'
  psql_restore -c "INSERT INTO \"__EFMigrationsHistory\" VALUES ('20261117155316_AddTeacherWhatsAppUrl');"
fi
test "$(protected_rows_hash)" = "$before"
validate_new_column
psql_restore -c "UPDATE teacher_profiles SET \"Bio\"='changed';"
test "$(protected_rows_hash)" != "$before"
psql_restore -c "UPDATE teacher_profiles SET \"Bio\"='original', \"WhatsAppUrl\"='https://whatsapp.com/channel/test';"
if test "$pre_teacher_whatsapp_exists" = 0; then
  if validate_new_column; then exit 8; fi
else
  test "$(protected_rows_hash)" != "$before"
fi
'''
        completed = subprocess.run(['bash'], input=script, text=True, env=env, capture_output=True)
        assert completed.returncode == 0, completed.stderr
    finally:
        subprocess.run(['dropdb', database], env=postgres, check=True, capture_output=True)


class TeacherProfileGateProjectionTests(unittest.TestCase):
    def test_nullable_column_preserves_old_values_and_still_detects_changes(self):
        with isolated_postgres() as postgres:
            for columns in [(), ('FinancePreset',), ('WhatsAppUrl',), ('FinancePreset', 'WhatsAppUrl')]:
                with self.subTest(existing_columns=columns):
                    verify_nullable_column(postgres, columns)


if __name__ == '__main__':
    unittest.main()
