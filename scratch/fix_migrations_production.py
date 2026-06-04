import paramiko
import sys
import re

def check_table_exists(ssh, table_name):
    query = f"SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_schema = 'public' AND table_name = '{table_name.lower()}');"
    cmd = f'docker exec -i nadergorge_db psql -U postgres -d nadergorge -t -A -c "{query}"'
    stdin, stdout, stderr = ssh.exec_command(cmd)
    out = stdout.read().decode('utf-8').strip()
    return out == "t"

def check_column_exists(ssh, table_name, column_name):
    query = f"SELECT EXISTS (SELECT FROM information_schema.columns WHERE table_schema = 'public' AND table_name = '{table_name.lower()}' AND column_name = '{column_name.lower()}');"
    cmd = f'docker exec -i nadergorge_db psql -U postgres -d nadergorge -t -A -c "{query}"'
    stdin, stdout, stderr = ssh.exec_command(cmd)
    out = stdout.read().decode('utf-8').strip()
    return out == "t"

def main():
    skip_build = "--skip-build" in sys.argv
    
    hostname = "72.62.27.189"
    username = "root"
    password = "MazenElsbagh.12"
    
    print("🚀 Connecting to production server...")
    ssh = paramiko.SSHClient()
    ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    
    try:
        ssh.connect(hostname, username=username, password=password, timeout=30)
        print("✅ Connected successfully to production server!")
    except Exception as e:
        print(f"❌ Failed to connect: {e}")
        sys.exit(1)

    # 1. Build and run containers if not skipped
    if not skip_build:
        print("🔨 Rebuilding and starting docker containers on production server...")
        build_cmd = "cd /var/www/nadergorge && docker compose up -d --build"
        
        stdin_b, stdout_b, stderr_b = ssh.exec_command(build_cmd)
        exit_status = stdout_b.channel.recv_exit_status()
        
        out = stdout_b.read().decode('utf-8', errors='replace')
        err = stderr_b.read().decode('utf-8', errors='replace')
        
        if exit_status != 0:
            print(f"❌ Failed to rebuild containers. Exit Code: {exit_status}")
            print("STDOUT:", out)
            print("STDERR:", err)
            ssh.close()
            sys.exit(1)
        else:
            print("✅ Docker containers rebuilt and started successfully!")
            if out:
                print(out.strip())

    # 2. Open SFTP to dynamically scan migrations in the repository on VPS
    sftp = ssh.open_sftp()
    migrations_dir = "/var/www/nadergorge/backend/src/NaderGorge.Infrastructure/Migrations"
    
    try:
        files = sftp.listdir(migrations_dir)
    except Exception as e:
        print(f"❌ Failed to list migrations directory: {e}")
        sftp.close()
        ssh.close()
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
            with sftp.file(f"{migrations_dir}/{filename}", "r") as f:
                content = f.read().decode('utf-8')
        except Exception as e:
            print(f"⚠️ Failed to read migration file {filename}: {e}. Skipping structural check.")
            continue
            
        # Parse table creations: CreateTable(name: "TableName"
        tables = re.findall(r'CreateTable\s*\(\s*name\s*:\s*"([^"]+)"', content, re.IGNORECASE)
        
        # Parse column additions: AddColumn<Type>(name: "ColumnName", table: "TableName"
        columns = re.findall(r'AddColumn\s*<[^>]+>\s*\(\s*name\s*:\s*"([^"]+)"\s*,\s*table\s*:\s*"([^"]+)"', content, re.IGNORECASE)
        
        # Check if the tables and columns exist in database schema
        for tbl in tables:
            if not check_table_exists(ssh, tbl):
                print(f"🔎 Migration '{migration_name}' creates table '{tbl}' which DOES NOT exist in DB.")
                is_applied = False
                break
                
        if not is_applied:
            break
            
        for col, tbl in columns:
            if not check_column_exists(ssh, tbl, col):
                print(f"🔎 Migration '{migration_name}' adds column '{col}' to '{tbl}' which DOES NOT exist in DB.")
                is_applied = False
                break
                
        if not is_applied:
            break
            
        # If we reached here, all structural changes in this migration are verified present
        verified_applied.append(migration_name)
        
    sftp.close()
    
    print(f"✅ Verified {len(verified_applied)} / {len(migration_files)} migrations as already structurally present in the database.")
    
    if len(verified_applied) > 0:
        # Construct the SQL query to seed verified migrations
        sql_create_table = 'CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" ("MigrationId" character varying(150) NOT NULL, "ProductVersion" character varying(32) NOT NULL, CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId"));\n'
        
        values_list = ", ".join([f"('{m}', '9.0.6')" for m in verified_applied])
        sql_insert_values = f'INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES {values_list} ON CONFLICT DO NOTHING;\n'
        
        combined_sql = f"{sql_create_table}{sql_insert_values}"
        
        # Seed the DB via stdin
        print("🛠  Populating __EFMigrationsHistory on production database (using stdin)...")
        db_cmd = 'docker exec -i nadergorge_db psql -U postgres -d nadergorge'
        
        stdin_db, stdout_db, stderr_db = ssh.exec_command(db_cmd)
        stdin_db.write(combined_sql)
        stdin_db.flush()
        stdin_db.channel.shutdown_write()
        
        exit_status = stdout_db.channel.recv_exit_status()
        out = stdout_db.read().decode('utf-8', errors='replace')
        err = stderr_db.read().decode('utf-8', errors='replace')
        
        if exit_status != 0:
            print(f"❌ Failed to seed __EFMigrationsHistory. Exit Code: {exit_status}")
            print("STDOUT:", out)
            print("STDERR:", err)
            ssh.close()
            sys.exit(1)
        else:
            print(f"✅ Seeding completed!")
            if out:
                print(out.strip())

    # 3. Run the EF Core migrator to apply whatever hasn't been structurally verified
    print("⚙️  Running pending migrations on production server...")
    migrator_cmd = "cd /var/www/nadergorge && docker compose --profile migration run --rm migrator"
    
    stdin_mig, stdout_mig, stderr_mig = ssh.exec_command(migrator_cmd)
    exit_status = stdout_mig.channel.recv_exit_status()
    
    out = stdout_mig.read().decode('utf-8', errors='replace')
    err = stderr_mig.read().decode('utf-8', errors='replace')
    
    print(f"Exit Status: {exit_status}")
    if out:
        print("STDOUT:")
        print(out.strip())
    if err:
        print("STDERR:")
        print(err.strip())
        
    ssh.close()
    if exit_status == 0:
        print("🎉 Production migrations fixed and applied successfully!")
    else:
        print("❌ Migrator returned errors on the server.")
        sys.exit(exit_status)

if __name__ == '__main__':
    main()
