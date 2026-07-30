using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NaderGorge.Infrastructure.Data;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations;

[Migration("20260721183000_BackfillParentTrackingCodes")]
[DbContext(typeof(AppDbContext))]
public partial class BackfillParentTrackingCodes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Parent tracking was added after some students already existed. Generate a
        // unique six-digit value for every legacy profile that does not have one.
        migrationBuilder.Sql("""
            DO $$
            DECLARE
                profile_record RECORD;
                candidate TEXT;
                next_value INTEGER := 100000;
            BEGIN
                FOR profile_record IN
                    SELECT "Id"
                    FROM student_profiles
                    WHERE "ParentTrackingCode" IS NULL OR BTRIM("ParentTrackingCode") = ''
                    ORDER BY "CreatedAt", "Id"
                LOOP
                    LOOP
                        candidate := LPAD(next_value::TEXT, 6, '0');
                        next_value := next_value + 1;
                        EXIT WHEN NOT EXISTS (
                            SELECT 1 FROM student_profiles WHERE "ParentTrackingCode" = candidate
                        );
                    END LOOP;

                    UPDATE student_profiles
                    SET "ParentTrackingCode" = candidate,
                        "UpdatedAt" = NOW()
                    WHERE "Id" = profile_record."Id";
                END LOOP;
            END $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Generated tracking codes are intentionally retained on rollback.
    }
}
