using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Themis.Migrations
{
    /// <inheritdoc />
    internal sealed partial class Foundations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "approvals");

            migrationBuilder.CreateTable(
                name: "alerts",
                schema: "approvals",
                columns: table => new
                {
                    condition = table.Column<string>(type: "text", nullable: false),
                    scope = table.Column<string>(type: "text", nullable: false),
                    holding = table.Column<bool>(type: "boolean", nullable: false),
                    last_raised_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    request = table.Column<Guid>(type: "uuid", nullable: true),
                    step = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_alerts", x => new { x.condition, x.scope });
                    table.CheckConstraint("ck_alerts_condition", "condition IN ('observer-failing', 'request-unassignable', 'job-stalled')");
                    table.CheckConstraint("ck_alerts_step", "step IS NULL OR step >= 1");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alerts",
                schema: "approvals");
        }
    }
}