using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixEventSubscriptionRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EventSubscriptions_Projects_ProjectId1",
                table: "EventSubscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_EventSubscriptions_WebhookEndpoints_EndpointId1",
                table: "EventSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_EventSubscriptions_EndpointId1",
                table: "EventSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_EventSubscriptions_ProjectId1",
                table: "EventSubscriptions");

            migrationBuilder.DropColumn(
                name: "EndpointId1",
                table: "EventSubscriptions");

            migrationBuilder.DropColumn(
                name: "ProjectId1",
                table: "EventSubscriptions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EndpointId1",
                table: "EventSubscriptions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId1",
                table: "EventSubscriptions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Restore valid relationship values before recreating the old constraints.
            migrationBuilder.Sql("""
                UPDATE "EventSubscriptions"
                SET "EndpointId1" = "EndpointId", "ProjectId1" = "ProjectId";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_EventSubscriptions_EndpointId1",
                table: "EventSubscriptions",
                column: "EndpointId1");

            migrationBuilder.CreateIndex(
                name: "IX_EventSubscriptions_ProjectId1",
                table: "EventSubscriptions",
                column: "ProjectId1");

            migrationBuilder.AddForeignKey(
                name: "FK_EventSubscriptions_Projects_ProjectId1",
                table: "EventSubscriptions",
                column: "ProjectId1",
                principalTable: "Projects",
                principalColumn: "ProjectId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EventSubscriptions_WebhookEndpoints_EndpointId1",
                table: "EventSubscriptions",
                column: "EndpointId1",
                principalTable: "WebhookEndpoints",
                principalColumn: "EndpointId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
