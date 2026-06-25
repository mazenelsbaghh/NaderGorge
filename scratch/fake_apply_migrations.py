import os
import sys
import subprocess

def run_cmd(cmd, input_data=None):
    if input_data:
        res = subprocess.run(cmd, shell=True, capture_output=True, text=True, input=input_data)
    else:
        res = subprocess.run(cmd, shell=True, capture_output=True, text=True)
    return res.returncode, res.stdout, res.stderr

def main():
    migrations_dir = "./backend/src/NaderGorge.Infrastructure/Migrations"
    if not os.path.exists(migrations_dir):
        print(f"❌ Migrations directory not found: {migrations_dir}")
        sys.exit(1)
        
    files = os.listdir(migrations_dir)
    migration_files = [f for f in files if f.endswith(".cs") and not f.endswith(".Designer.cs") and f != "AppDbContextModelSnapshot.cs"]
    migration_files.sort()
    
    if not migration_files:
        print("❌ No migration files found.")
        sys.exit(1)

    # We want to apply everything EXCEPT the newest one
    newest_migration = migration_files[-1][:-3]
    to_seed = []
    
    for filename in migration_files:
        name = filename[:-3]
        if name == newest_migration:
            continue
        to_seed.append(name)
        
    print(f"Applying fake history for {len(to_seed)} migrations (excluding newest: {newest_migration})...")
    
    sql_create_table = 'CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" ("MigrationId" character varying(150) NOT NULL, "ProductVersion" character varying(32) NOT NULL, CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId"));\n'
    values_list = ", ".join([f"('{m}', '9.0.6')" for m in to_seed])
    sql_insert_values = f'INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES {values_list} ON CONFLICT DO NOTHING;\n'
    combined_sql = f"{sql_create_table}{sql_insert_values}"
    
    db_cmd = 'docker exec -i massar_db psql -U postgres -d massar_platform_test'
    code, out, err = run_cmd(db_cmd, input_data=combined_sql)
    
    if code != 0:
        print(f"❌ Failed to seed __EFMigrationsHistory. Exit Code: {code}")
        print("STDOUT:", out)
        print("STDERR:", err)
        sys.exit(1)
    else:
        print("✅ Seeded __EFMigrationsHistory successfully!")
        
    # Now run make migrate
    print("Running make migrate...")
    code, out, err = run_cmd("make migrate")
    print(f"make migrate Exit Code: {code}")
    print("STDOUT:", out)
    print("STDERR:", err)

if __name__ == '__main__':
    main()
