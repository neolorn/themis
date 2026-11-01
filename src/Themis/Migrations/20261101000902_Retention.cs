using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Themis.Migrations
{
    /// <inheritdoc />
    internal sealed partial class Retention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_jobs_name",
                schema: "approvals",
                table: "jobs");

            migrationBuilder.InsertData(
                schema: "approvals",
                table: "jobs",
                columns: new[] { "name", "claim_expires_at", "holder", "last_completed_at", "last_renewed_at", "last_started_at" },
                values: new object[] { "retention", null, null, null, null, null });

            migrationBuilder.AddCheckConstraint(
                name: "ck_jobs_name",
                schema: "approvals",
                table: "jobs",
                sql: "name IN ('deadlines', 'observer', 'watch', 'retention')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_jobs_name",
                schema: "approvals",
                table: "jobs");

            migrationBuilder.DeleteData(
                schema: "approvals",
                table: "jobs",
                keyColumn: "name",
                keyValue: "retention");

            migrationBuilder.AddCheckConstraint(
                name: "ck_jobs_name",
                schema: "approvals",
                table: "jobs",
                sql: "name IN ('deadlines', 'observer', 'watch')");
        }
    }
}