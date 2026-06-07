import sys
import os
import re
import subprocess

def run_cmd(cmd, input_data=None):
    if input_data:
        res = subprocess.run(cmd, shell=True, capture_output=True, text=True, input=input_data)
    else:
        res = subprocess.run(cmd, shell=True, capture_output=True, text=True)
    return res.returncode, res.stdout, res.stderr

def check_table_exists(table_name):
    query = f"SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_schema = 'public' AND table_name = '{table_name}');"
    cmd = f'docker exec -i massar_db psql -U postgres -d nadergorge -t -A -c "{query}"'
    code, out, err = run_cmd(cmd)
    return out.strip() == "t"

def check_column_exists(table_name, column_name):
    query = f"SELECT EXISTS (SELECT FROM information_schema.columns WHERE table_schema = 'public' AND table_name = '{table_name}' AND column_name = '{column_name}');"
    cmd = f'docker exec -i massar_db psql -U postgres -d nadergorge -t -A -c "{query}"'
    code, out, err = run_cmd(cmd)
    return out.strip() == "t"

def main():
    skip_build = "--skip-build" in sys.argv
    
    # 0. Apply missing tables/columns from docker/create_missing_tables.sql
    print("🛠 Running docker/create_missing_tables.sql to ensure missing tables exist on production...")
    sql_path = "/var/www/nadergorge/docker/create_missing_tables.sql"
    if os.path.exists(sql_path):
        try:
            with open(sql_path, "r") as f:
                create_tables_sql = f.read()
            
            db_cmd = 'docker exec -i massar_db psql -U postgres -d nadergorge'
            code, out, err = run_cmd(db_cmd, input_data=create_tables_sql)
            
            if code != 0:
                print(f"❌ Failed to run create_missing_tables.sql. Exit Code: {code}")
                print("STDOUT:", out)
                print("STDERR:", err)
                sys.exit(1)
            else:
                print("✅ create_missing_tables.sql applied successfully on production database!")
                if out.strip():
                    print(out.strip())
        except Exception as e:
            print(f"❌ Error applying missing tables script: {e}")
            sys.exit(1)
    else:
        print(f"⚠️ Warning: {sql_path} not found. Skipping initial SQL application.")

    # 1. Build and run containers if not skipped
    if not skip_build:
        print("🔨 Rebuilding and starting docker containers on production server...")
        build_cmd = "cd /var/www/nadergorge && docker compose up -d --build"
        code, out, err = run_cmd(build_cmd)
        
        if code != 0:
            print(f"❌ Failed to rebuild containers. Exit Code: {code}")
            print("STDOUT:", out)
            print("STDERR:", err)
            sys.exit(1)
        else:
            print("✅ Docker containers rebuilt and started successfully!")
            if out.strip():
                print(out.strip())

    # 2. Dynamically scan migrations in the repository
    migrations_dir = "/var/www/nadergorge/backend/src/NaderGorge.Infrastructure/Migrations"
    
    if not os.path.exists(migrations_dir):
        print(f"❌ Migrations directory not found: {migrations_dir}")
        sys.exit(1)
        
    try:
        files = os.listdir(migrations_dir)
    except Exception as e:
        print(f"❌ Failed to list migrations directory: {e}")
        sys.exit(1)
        
    # Filter for migration files (*.cs, excluding .Designer.cs and snapshot)
    migration_files = [f for f in files if f.endswith(".cs") and not f.endswith(".Designer.cs") and f != "AppDbContextModelSnapshot.cs"]
    migration_files.sort()  # Sort chronologically by timestamp prefix
    
    print(f"📂 Found {len(migration_files)} migration files in codebase.")
    
    verified_applied = []
    is_applied = True
    
    for filename in migration_files:
        migration_name = filename[:-3]  # Strip ".cs"
        
        # Read the migration code
        try:
            with open(os.path.join(migrations_dir, filename), "r") as f:
                content = f.read()
        except Exception as e:
            print(f"⚠️ Failed to read migration file {filename}: {e}. Skipping structural check.")
            continue
            
        # Parse table creations: CreateTable(name: "TableName"
        tables = re.findall(r'CreateTable\s*\(\s*name\s*:\s*"([^"]+)"', content, re.IGNORECASE)
        
        # Parse column additions: AddColumn<Type>(name: "ColumnName", table: "TableName"
        columns = re.findall(r'AddColumn\s*<[^>]+>\s*\(\s*name\s*:\s*"([^"]+)"\s*,\s*table\s*:\s*"([^"]+)"', content, re.IGNORECASE)
        
        # Check if the tables and columns exist in database schema
        for tbl in tables:
            if not check_table_exists(tbl):
                print(f"🔎 Migration '{migration_name}' creates table '{tbl}' which DOES NOT exist in DB.")
                is_applied = False
                break
                
        if not is_applied:
            break
            
        for col, tbl in columns:
            if not check_column_exists(tbl, col):
                print(f"🔎 Migration '{migration_name}' adds column '{col}' to '{tbl}' which DOES NOT exist in DB.")
                is_applied = False
                break
                
        if not is_applied:
            break
            
        # If we reached here, all structural changes in this migration are verified present
        verified_applied.append(migration_name)
        
    print(f"✅ Verified {len(verified_applied)} / {len(migration_files)} migrations as already structurally present in the database.")
    
    if len(verified_applied) > 0:
        # Construct the SQL query to seed verified migrations
        sql_create_table = 'CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" ("MigrationId" character varying(150) NOT NULL, "ProductVersion" character varying(32) NOT NULL, CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId"));\n'
        
        values_list = ", ".join([f"('{m}', '9.0.6')" for m in verified_applied])
        sql_insert_values = f'INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES {values_list} ON CONFLICT DO NOTHING;\n'
        
        combined_sql = f"{sql_create_table}{sql_insert_values}"
        
        # Seed the DB via stdin
        print("🛠  Populating __EFMigrationsHistory on production database (using stdin)...")
        db_cmd = 'docker exec -i massar_db psql -U postgres -d nadergorge'
        
        code, out, err = run_cmd(db_cmd, input_data=combined_sql)
        
        if code != 0:
            print(f"❌ Failed to seed __EFMigrationsHistory. Exit Code: {code}")
            print("STDOUT:", out)
            print("STDERR:", err)
            sys.exit(1)
        else:
            print(f"✅ Seeding completed!")
            if out.strip():
                print(out.strip())

    # 3. Run the EF Core migrator to apply whatever hasn't been structurally verified
    print("⚙️  Running pending migrations on production server...")
    migrator_cmd = "cd /var/www/nadergorge && docker compose --profile migration run --rm migrator"
    
    code, out, err = run_cmd(migrator_cmd)
    
    print(f"Exit Status: {code}")
    if out.strip():
        print("STDOUT:")
        print(out.strip())
    if err.strip():
        print("STDERR:")
        print(err.strip())
        
    if code == 0:
        print("🎉 Production migrations fixed and applied successfully!")
    else:
        print("❌ Migrator returned errors on the server.")
        sys.exit(code)

if __name__ == '__main__':
    main()
