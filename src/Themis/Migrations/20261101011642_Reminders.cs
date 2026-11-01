using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Themis.Migrations
{
    /// <inheritdoc />
    internal sealed partial class Reminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_events_kind",
                schema: "approvals",
                table: "events");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "remind_at",
                schema: "approvals",
                table: "steps",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "reminders_sent",
                schema: "approvals",
                table: "steps",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "ix_steps_remind",
                schema: "approvals",
                table: "steps",
                column: "remind_at",
                filter: "status = 'current' AND deadline_ran_at IS NULL AND remind_at IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_events_kind",
                schema: "approvals",
                table: "events",
                sql: "kind IN ('requested', 'stepstarted', 'escalated', 'approved', 'rejected', 'cancelled', 'voided', 'completed', 'delegationcreated', 'reminded')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_steps_remind",
                schema: "approvals",
                table: "steps");

            migrationBuilder.DropCheckConstraint(
                name: "ck_events_kind",
                schema: "approvals",
                table: "events");

            migrationBuilder.DropColumn(
                name: "remind_at",
                schema: "approvals",
                table: "steps");

            migrationBuilder.DropColumn(
                name: "reminders_sent",
                schema: "approvals",
                table: "steps");

            migrationBuilder.AddCheckConstraint(
                name: "ck_events_kind",
                schema: "approvals",
                table: "events",
                sql: "kind IN ('requested', 'stepstarted', 'escalated', 'approved', 'rejected', 'cancelled', 'voided', 'completed', 'delegationcreated')");
        }
    }
}