using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NaderGorge.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompleteAILiveSupportProduction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_live_support_ai_verification_sessions_ConversationId",
                table: "live_support_ai_verification_sessions");

            migrationBuilder.AddColumn<int>(
                name: "CurrentQuestionIndex",
                table: "live_support_ai_verification_sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAttemptAt",
                table: "live_support_ai_verification_sessions",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockedAt",
                table: "live_support_ai_verification_sessions",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CallbackAttemptCount",
                table: "live_support_ai_turns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CallbackStatus",
                table: "live_support_ai_turns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DecisionHash",
                table: "live_support_ai_turns",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastSafeCallbackErrorCode",
                table: "live_support_ai_turns",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextCallbackAttemptAt",
                table: "live_support_ai_turns",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProviderCompletedAt",
                table: "live_support_ai_turns",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "StudentUserId",
                table: "live_support_ai_pending_actions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "CallbackDecisionHash",
                table: "live_support_ai_pending_actions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "live_support_ai_pending_actions",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DecisionKind",
                table: "live_support_ai_pending_actions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "DisableRequestedAt",
                table: "live_support_ai_conversation_states",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LastEventSequence",
                table: "live_support_ai_conversation_states",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastRecoveryAt",
                table: "live_support_ai_conversation_states",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE live_support_ai_pending_actions
                SET "DecisionKind" = CASE
                    WHEN "ActionKey" = 'system.handoff' THEN 1
                    WHEN "ActionKey" = 'system.account-creation' THEN 2
                    WHEN "ActionKey" = 'system.resolution' THEN 3
                    ELSE 0
                END;

                UPDATE live_support_ai_pending_actions
                SET "StudentUserId" = NULL
                WHERE "DecisionKind" <> 0
                   OR "StudentUserId" = '00000000-0000-0000-0000-000000000000';

                WITH ranked AS (
                    SELECT "Id",
                           row_number() OVER (
                               PARTITION BY "ConversationId", "DecisionKind"
                               ORDER BY "CreatedAt" DESC, "Id" DESC
                           ) AS row_number
                    FROM live_support_ai_pending_actions
                    WHERE "Status" = 0
                )
                UPDATE live_support_ai_pending_actions AS pending
                SET "Status" = 4,
                    "FailureCode" = 'MIGRATION_DUPLICATE_INVALIDATED'
                FROM ranked
                WHERE pending."Id" = ranked."Id"
                  AND ranked.row_number > 1;

                UPDATE live_support_ai_pending_actions
                SET "Status" = 4,
                    "FailureCode" = 'MIGRATION_UNSAFE_PAYLOAD_INVALIDATED',
                    "PayloadHash" = repeat('0', 64),
                    "StateFingerprint" = repeat('0', 64),
                    "ConfirmationNonceHash" = repeat('0', 64),
                    "EncryptedPayload" = decode('', 'hex')
                WHERE "DecisionKind" = 0
                  AND (
                      "EncryptedPayload" IS NULL
                      OR length("PayloadHash") = 0
                      OR length("StateFingerprint") = 0
                      OR length("ConfirmationNonceHash") = 0
                  );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ai_verification_sessions_ConversationId",
                table: "live_support_ai_verification_sessions",
                column: "ConversationId",
                unique: true,
                filter: "\"Status\" IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ai_verification_sessions_Status_ExpiresAt",
                table: "live_support_ai_verification_sessions",
                columns: new[] { "Status", "ExpiresAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_live_support_ai_verification_counts",
                table: "live_support_ai_verification_sessions",
                sql: "\"CorrectCount\" >= 0 AND \"CorrectCount\" <= \"AttemptCount\" AND \"AttemptCount\" <= \"MaxAttempts\" AND \"CurrentQuestionIndex\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ai_turns_CallbackStatus_NextCallbackAttemptAt",
                table: "live_support_ai_turns",
                columns: new[] { "CallbackStatus", "NextCallbackAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ai_pending_actions_ConversationId_DecisionKind",
                table: "live_support_ai_pending_actions",
                columns: new[] { "ConversationId", "DecisionKind" },
                unique: true,
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ai_pending_actions_Status_ExpiresAt",
                table: "live_support_ai_pending_actions",
                columns: new[] { "Status", "ExpiresAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_live_support_ai_pending_action_target",
                table: "live_support_ai_pending_actions",
                sql: "\"DecisionKind\" <> 0 OR (\"StudentUserId\" IS NOT NULL AND length(\"ActionKey\") > 0 AND length(\"PayloadHash\") > 0 AND length(\"StateFingerprint\") > 0 AND \"EncryptedPayload\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM live_support_ai_pending_actions
                        WHERE "StudentUserId" IS NULL
                    ) THEN
                        RAISE EXCEPTION 'Cannot downgrade AI live support while non-action decisions with nullable student targets exist. Disable AI and retain the additive schema.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropIndex(
                name: "IX_live_support_ai_verification_sessions_ConversationId",
                table: "live_support_ai_verification_sessions");

            migrationBuilder.DropIndex(
                name: "IX_live_support_ai_verification_sessions_Status_ExpiresAt",
                table: "live_support_ai_verification_sessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_live_support_ai_verification_counts",
                table: "live_support_ai_verification_sessions");

            migrationBuilder.DropIndex(
                name: "IX_live_support_ai_turns_CallbackStatus_NextCallbackAttemptAt",
                table: "live_support_ai_turns");

            migrationBuilder.DropIndex(
                name: "IX_live_support_ai_pending_actions_ConversationId_DecisionKind",
                table: "live_support_ai_pending_actions");

            migrationBuilder.DropIndex(
                name: "IX_live_support_ai_pending_actions_Status_ExpiresAt",
                table: "live_support_ai_pending_actions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_live_support_ai_pending_action_target",
                table: "live_support_ai_pending_actions");

            migrationBuilder.DropColumn(
                name: "CurrentQuestionIndex",
                table: "live_support_ai_verification_sessions");

            migrationBuilder.DropColumn(
                name: "LastAttemptAt",
                table: "live_support_ai_verification_sessions");

            migrationBuilder.DropColumn(
                name: "LockedAt",
                table: "live_support_ai_verification_sessions");

            migrationBuilder.DropColumn(
                name: "CallbackAttemptCount",
                table: "live_support_ai_turns");

            migrationBuilder.DropColumn(
                name: "CallbackStatus",
                table: "live_support_ai_turns");

            migrationBuilder.DropColumn(
                name: "DecisionHash",
                table: "live_support_ai_turns");

            migrationBuilder.DropColumn(
                name: "LastSafeCallbackErrorCode",
                table: "live_support_ai_turns");

            migrationBuilder.DropColumn(
                name: "NextCallbackAttemptAt",
                table: "live_support_ai_turns");

            migrationBuilder.DropColumn(
                name: "ProviderCompletedAt",
                table: "live_support_ai_turns");

            migrationBuilder.DropColumn(
                name: "CallbackDecisionHash",
                table: "live_support_ai_pending_actions");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "live_support_ai_pending_actions");

            migrationBuilder.DropColumn(
                name: "DecisionKind",
                table: "live_support_ai_pending_actions");

            migrationBuilder.DropColumn(
                name: "DisableRequestedAt",
                table: "live_support_ai_conversation_states");

            migrationBuilder.DropColumn(
                name: "LastEventSequence",
                table: "live_support_ai_conversation_states");

            migrationBuilder.DropColumn(
                name: "LastRecoveryAt",
                table: "live_support_ai_conversation_states");

            migrationBuilder.AlterColumn<Guid>(
                name: "StudentUserId",
                table: "live_support_ai_pending_actions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_live_support_ai_verification_sessions_ConversationId",
                table: "live_support_ai_verification_sessions",
                column: "ConversationId",
                unique: true);
        }
    }
}
