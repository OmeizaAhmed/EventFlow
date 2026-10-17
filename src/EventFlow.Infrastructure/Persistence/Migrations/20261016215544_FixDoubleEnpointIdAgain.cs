using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixDoubleEnpointIdAgain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EventSubscriptions_Projects_EndpointId",
                table: "EventSubscriptions");

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId1",
                table: "EventSubscriptions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EventSubscriptions_Projects_ProjectId1",
                table: "EventSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_EventSubscriptions_ProjectId1",
                table: "EventSubscriptions");

            migrationBuilder.DropColumn(
                name: "ProjectId1",
                table: "EventSubscriptions");

            migrationBuilder.AddForeignKey(
                name: "FK_EventSubscriptions_Projects_EndpointId",
                table: "EventSubscriptions",
                column: "EndpointId",
                principalTable: "Projects",
                principalColumn: "ProjectId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
