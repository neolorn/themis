using System;
using System.Collections.Generic;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Themis.Migrations
{
    /// <inheritdoc />
    internal sealed partial class Delegations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "delegations",
                schema: "approvals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_person = table.Column<string>(type: "text", nullable: false),
                    to_person = table.Column<string>(type: "text", nullable: false),
                    starts = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    types = table.Column<List<string>>(type: "text[]", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ended_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_delegations", x => x.id);
                    table.CheckConstraint("ck_delegations_period", "ends > starts");
                    table.CheckConstraint("ck_delegations_persons", "to_person <> from_person");
                });

            migrationBuilder.CreateIndex(
                name: "IX_events_delegation",
                schema: "approvals",
                table: "events",
                column: "delegation");

            migrationBuilder.CreateIndex(
                name: "ix_delegations_from",
                schema: "approvals",
                table: "delegations",
                column: "from_person",
                filter: "ended_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_delegations_to",
                schema: "approvals",
                table: "delegations",
                column: "to_person",
                filter: "ended_at IS NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_events_delegations",
                schema: "approvals",
                table: "events",
                column: "delegation",
                principalSchema: "approvals",
                principalTable: "delegations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_events_delegations",
                schema: "approvals",
                table: "events");

            migrationBuilder.DropTable(
                name: "delegations",
                schema: "approvals");

            migrationBuilder.DropIndex(
                name: "IX_events_delegation",
                schema: "approvals",
                table: "events");
        }
    }
}