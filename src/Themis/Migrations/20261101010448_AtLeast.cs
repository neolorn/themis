using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Themis.Migrations
{
    /// <inheritdoc />
    internal sealed partial class AtLeast : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_steps_mode",
                schema: "approvals",
                table: "steps");

            migrationBuilder.AddColumn<int>(
                name: "required",
                schema: "approvals",
                table: "steps",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_steps_mode",
                schema: "approvals",
                table: "steps",
                sql: "mode IN ('anyone', 'all', 'atleast')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_steps_required",
                schema: "approvals",
                table: "steps",
                sql: "(mode = 'atleast' AND required BETWEEN 2 AND 16) OR (mode <> 'atleast' AND required IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_steps_mode",
                schema: "approvals",
                table: "steps");

            migrationBuilder.DropCheckConstraint(
                name: "ck_steps_required",
                schema: "approvals",
                table: "steps");

            migrationBuilder.DropColumn(
                name: "required",
                schema: "approvals",
                table: "steps");

            migrationBuilder.AddCheckConstraint(
                name: "ck_steps_mode",
                schema: "approvals",
                table: "steps",
                sql: "mode IN ('anyone', 'all')");
        }
    }
}