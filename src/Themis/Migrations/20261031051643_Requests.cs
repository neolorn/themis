using System;
using System.Collections.Generic;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Themis.Migrations
{
    /// <inheritdoc />
    internal sealed partial class Requests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "jobs",
                schema: "approvals",
                columns: table => new
                {
                    name = table.Column<string>(type: "text", nullable: false),
                    holder = table.Column<string>(type: "text", nullable: true),
                    claim_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_renewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_jobs", x => x.name);
                    table.CheckConstraint("ck_jobs_name", "name IN ('deadlines', 'observer', 'watch')");
                });

            migrationBuilder.CreateTable(
                name: "requests",
                schema: "approvals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    policy = table.Column<string>(type: "text", nullable: true),
                    target_kind = table.Column<string>(type: "text", nullable: false),
                    target_id = table.Column<string>(type: "text", nullable: false),
                    snapshot = table.Column<string>(type: "text", nullable: false),
                    requester = table.Column<string>(type: "text", nullable: false),
                    subject = table.Column<string>(type: "text", nullable: true),
                    requester_roles = table.Column<List<string>>(type: "text[]", nullable: false),
                    subject_roles = table.Column<List<string>>(type: "text[]", nullable: false),
                    previous = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    step = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_by = table.Column<string>(type: "text", nullable: true),
                    closed_for = table.Column<string>(type: "text", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    reason_code = table.Column<string>(type: "text", nullable: true),
                    changed_field = table.Column<string>(type: "text", nullable: true),
                    sequence = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_requests", x => x.id);
                    table.CheckConstraint("ck_requests_reason_code", "reason_code IS NULL OR reason_code IN ('noapprover', 'deadline', 'fieldchanged')");
                    table.CheckConstraint("ck_requests_status", "status IN ('pending', 'approved', 'rejected', 'cancelled', 'void', 'completed')");
                    table.CheckConstraint("ck_requests_step", "step IS NULL OR step >= 1");
                    table.ForeignKey(
                        name: "fk_requests_previous",
                        column: x => x.previous,
                        principalSchema: "approvals",
                        principalTable: "requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_requests_versions",
                        columns: x => new { x.type, x.version },
                        principalSchema: "approvals",
                        principalTable: "versions",
                        principalColumns: new[] { "key", "version" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "events",
                schema: "approvals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request = table.Column<Guid>(type: "uuid", nullable: true),
                    sequence = table.Column<long>(type: "bigint", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    type = table.Column<string>(type: "text", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: true),
                    target_kind = table.Column<string>(type: "text", nullable: true),
                    target_id = table.Column<string>(type: "text", nullable: true),
                    step = table.Column<int>(type: "integer", nullable: true),
                    actor = table.Column<string>(type: "text", nullable: true),
                    acting_for = table.Column<string>(type: "text", nullable: true),
                    reason_code = table.Column<string>(type: "text", nullable: true),
                    delegation = table.Column<Guid>(type: "uuid", nullable: true),
                    notify = table.Column<List<string>>(type: "text[]", nullable: false),
                    taken_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_events", x => x.id);
                    table.CheckConstraint("ck_events_kind", "kind IN ('requested', 'stepstarted', 'escalated', 'approved', 'rejected', 'cancelled', 'voided', 'completed', 'delegationcreated')");
                    table.CheckConstraint("ck_events_reason_code", "reason_code IS NULL OR reason_code IN ('noapprover', 'deadline', 'fieldchanged')");
                    table.CheckConstraint("ck_events_sequence", "sequence >= 1");
                    table.ForeignKey(
                        name: "fk_events_requests",
                        column: x => x.request,
                        principalSchema: "approvals",
                        principalTable: "requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "steps",
                schema: "approvals",
                columns: table => new
                {
                    request = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    mode = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deadline_ran_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_steps", x => new { x.request, x.number });
                    table.CheckConstraint("ck_steps_mode", "mode IN ('anyone', 'all')");
                    table.CheckConstraint("ck_steps_number", "number >= 1");
                    table.CheckConstraint("ck_steps_status", "status IN ('waiting', 'current', 'done', 'skipped', 'closed')");
                    table.ForeignKey(
                        name: "fk_steps_requests",
                        column: x => x.request,
                        principalSchema: "approvals",
                        principalTable: "requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "decisions",
                schema: "approvals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request = table.Column<Guid>(type: "uuid", nullable: false),
                    step = table.Column<int>(type: "integer", nullable: false),
                    decider = table.Column<string>(type: "text", nullable: false),
                    acting_for = table.Column<string>(type: "text", nullable: true),
                    outcome = table.Column<string>(type: "text", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_decisions", x => x.id);
                    table.CheckConstraint("ck_decisions_outcome", "outcome IN ('approve', 'reject')");
                    table.ForeignKey(
                        name: "fk_decisions_steps",
                        columns: x => new { x.request, x.step },
                        principalSchema: "approvals",
                        principalTable: "steps",
                        principalColumns: new[] { "request", "number" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "assignments",
                schema: "approvals",
                columns: table => new
                {
                    request = table.Column<Guid>(type: "uuid", nullable: false),
                    step = table.Column<int>(type: "integer", nullable: false),
                    person = table.Column<string>(type: "text", nullable: false),
                    origin = table.Column<string>(type: "text", nullable: false),
                    via = table.Column<string>(type: "text", nullable: false),
                    decision = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assignments", x => new { x.request, x.step, x.person });
                    table.CheckConstraint("ck_assignments_origin", "origin IN ('approver', 'fallback', 'escalation')");
                    table.ForeignKey(
                        name: "fk_assignments_decisions",
                        column: x => x.decision,
                        principalSchema: "approvals",
                        principalTable: "decisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_assignments_steps",
                        columns: x => new { x.request, x.step },
                        principalSchema: "approvals",
                        principalTable: "steps",
                        principalColumns: new[] { "request", "number" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "approvals",
                table: "jobs",
                columns: new[] { "name", "claim_expires_at", "holder", "last_completed_at", "last_renewed_at", "last_started_at" },
                values: new object[,]
                {
                    { "deadlines", null, null, null, null, null },
                    { "observer", null, null, null, null, null },
                    { "watch", null, null, null, null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_assignments_decision",
                schema: "approvals",
                table: "assignments",
                column: "decision");

            migrationBuilder.CreateIndex(
                name: "ix_assignments_person",
                schema: "approvals",
                table: "assignments",
                column: "person",
                filter: "decision IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_decisions_decider",
                schema: "approvals",
                table: "decisions",
                columns: new[] { "request", "step", "decider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_events_untaken",
                schema: "approvals",
                table: "events",
                column: "at",
                filter: "taken_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_events_sequence",
                schema: "approvals",
                table: "events",
                columns: new[] { "request", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_requests_previous",
                schema: "approvals",
                table: "requests",
                column: "previous");

            migrationBuilder.CreateIndex(
                name: "ix_requests_target",
                schema: "approvals",
                table: "requests",
                columns: new[] { "target_kind", "target_id" });

            migrationBuilder.CreateIndex(
                name: "IX_requests_type_version",
                schema: "approvals",
                table: "requests",
                columns: new[] { "type", "version" });

            migrationBuilder.CreateIndex(
                name: "ux_requests_open",
                schema: "approvals",
                table: "requests",
                columns: new[] { "type", "target_kind", "target_id" },
                unique: true,
                filter: "status IN ('pending', 'approved')");

            migrationBuilder.CreateIndex(
                name: "ix_steps_due",
                schema: "approvals",
                table: "steps",
                column: "due_at",
                filter: "status = 'current' AND deadline_ran_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assignments",
                schema: "approvals");

            migrationBuilder.DropTable(
                name: "events",
                schema: "approvals");

            migrationBuilder.DropTable(
                name: "jobs",
                schema: "approvals");

            migrationBuilder.DropTable(
                name: "decisions",
                schema: "approvals");

            migrationBuilder.DropTable(
                name: "steps",
                schema: "approvals");

            migrationBuilder.DropTable(
                name: "requests",
                schema: "approvals");
        }
    }
}