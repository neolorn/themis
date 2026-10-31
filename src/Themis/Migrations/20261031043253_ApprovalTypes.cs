using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Themis.Migrations
{
    /// <inheritdoc />
    internal sealed partial class ApprovalTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "types",
                schema: "approvals",
                columns: table => new
                {
                    key = table.Column<string>(type: "text", nullable: false),
                    retired = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_types", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "versions",
                schema: "approvals",
                columns: table => new
                {
                    key = table.Column<string>(type: "text", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    definition = table.Column<string>(type: "text", nullable: false),
                    hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_versions", x => new { x.key, x.version });
                    table.CheckConstraint("ck_versions_hash", "octet_length(hash) = 32");
                    table.CheckConstraint("ck_versions_version", "version >= 1");
                    table.ForeignKey(
                        name: "fk_versions_types",
                        column: x => x.key,
                        principalSchema: "approvals",
                        principalTable: "types",
                        principalColumn: "key",
                        onDelete: ReferentialAction.Restrict);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "versions",
                schema: "approvals");

            migrationBuilder.DropTable(
                name: "types",
                schema: "approvals");
        }
    }
}