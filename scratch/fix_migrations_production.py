import paramiko
import sys

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

    # List of 31 migrations up to 20260603201648_AddCustomFormVisitCount (excluding unapplied ones)
    migrations = [
        "20260323154931_InitialCreate",
        "20260323155605_AddUS1CodeEntities",
        "20260323160227_AddUS2ContentTrackingEntities",
        "20260323161056_AddUS3ExamEntities",
        "20260325211038_AddVideoPlaybackSession",
        "20260326000830_AddPhase2AcademicOps",
        "20260328000545_AddPhase3TermsAndCodes",
        "20260328021623_AddRegistrationFieldUpdates",
        "20260328034522_AddPackageIsActive",
        "20260328041251_AddContentPricing",
        "20260328045309_InlineExamsAndQuestions",
        "20260328050813_AssessmentGradingUpdate",
        "20260328052701_AddExamTimersAndDashboard",
        "20260328061908_AddTimePerQuestionSecondsToExam",
        "20260330040115_StudentProfileV2",
        "20260331131211_UnifiedAssessmentBuilder",
        "20260331173238_AddVideoChapters",
        "20260401114742_AddChapterMindmapGeneration",
        "20260401120228_AddMindmapImageUrlToVideoChapters",
        "20260401121132_AddIsProcessingMindmapsToLessonVideo",
        "20260408164959_AddLessonCommentsModeration",
        "20260408171549_AddStudentCommunity",
        "20260408175220_AddStudentThemePreferences",
        "20260408190000_AddPackageCodePageProfiles",
        "20260409141216_AddCommunityCommentModerationAndCriticalExamFixes",
        "20260418174224_RemoveQuestionDuration",
        "20260419214734_AddExamDisplayQuestionCount",
        "20260601181311_AddStudentAvatarSlug",
        "20260601200420_AddCustomFormsAndSubmissions",
        "20260603181708_RemoveBunnyTelegramProviders",
        "20260603201648_AddCustomFormVisitCount"
    ]

    # Construct the SQL query
    sql_create_table = 'CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" ("MigrationId" character varying(150) NOT NULL, "ProductVersion" character varying(32) NOT NULL, CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId"));\n'
    
    values_list = ", ".join([f"('{m}', '9.0.0')" for m in migrations])
    sql_insert_values = f'INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES {values_list} ON CONFLICT DO NOTHING;\n'
    
    # Combined SQL statement
    combined_sql = f"{sql_create_table}{sql_insert_values}"
    
    # Run the SQL statement inside nadergorge_db container via stdin
    print("🛠  Populating __EFMigrationsHistory on production database (using stdin)...")
    db_cmd = 'docker exec -i nadergorge_db psql -U postgres -d nadergorge'
    
    stdin_db, stdout_db, stderr_db = ssh.exec_command(db_cmd)
    stdin_db.write(combined_sql)
    stdin_db.flush()
    stdin_db.channel.shutdown_write()  # Signal EOF to psql
    
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
        print("✅ Successfully seeded 31 migrations in __EFMigrationsHistory table!")
        if out:
            print(out.strip())

    # Now run the migrator container on VPS to apply any pending migrations
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
