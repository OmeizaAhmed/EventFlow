using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteEntityRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMemberships_InvitedByUserId",
                table: "ProjectMemberships",
                column: "InvitedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMemberships_LastModifiedByUserId",
                table: "ProjectMemberships",
                column: "LastModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DomainUsers_AuthId",
                table: "DomainUsers",
                column: "AuthId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_EndpointId",
                table: "Deliveries",
                column: "EndpointId");

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_EventId",
                table: "Deliveries",
                column: "EventId");

            migrationBuilder.AddForeignKey(
                name: "FK_Deliveries_Events_EventId",
                table: "Deliveries",
                column: "EventId",
                principalTable: "Events",
                principalColumn: "EventId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Deliveries_WebhookEndpoints_EndpointId",
                table: "Deliveries",
                column: "EndpointId",
                principalTable: "WebhookEndpoints",
                principalColumn: "EndpointId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DomainUsers_AspNetUsers_AuthId",
                table: "DomainUsers",
                column: "AuthId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Events_Projects_ProjectId",
                table: "Events",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "ProjectId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectMemberships_DomainUsers_InvitedByUserId",
                table: "ProjectMemberships",
                column: "InvitedByUserId",
                principalTable: "DomainUsers",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectMemberships_DomainUsers_LastModifiedByUserId",
                table: "ProjectMemberships",
                column: "LastModifiedByUserId",
                principalTable: "DomainUsers",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshTokens_DomainUsers_UserId",
                table: "RefreshTokens",
                column: "UserId",
                principalTable: "DomainUsers",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Deliveries_Events_EventId",
                table: "Deliveries");

            migrationBuilder.DropForeignKey(
                name: "FK_Deliveries_WebhookEndpoints_EndpointId",
                table: "Deliveries");

            migrationBuilder.DropForeignKey(
                name: "FK_DomainUsers_AspNetUsers_AuthId",
                table: "DomainUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_Events_Projects_ProjectId",
                table: "Events");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectMemberships_DomainUsers_InvitedByUserId",
                table: "ProjectMemberships");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectMemberships_DomainUsers_LastModifiedByUserId",
                table: "ProjectMemberships");

            migrationBuilder.DropForeignKey(
                name: "FK_RefreshTokens_DomainUsers_UserId",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_ProjectMemberships_InvitedByUserId",
                table: "ProjectMemberships");

            migrationBuilder.DropIndex(
                name: "IX_ProjectMemberships_LastModifiedByUserId",
                table: "ProjectMemberships");

            migrationBuilder.DropIndex(
                name: "IX_DomainUsers_AuthId",
                table: "DomainUsers");

            migrationBuilder.DropIndex(
                name: "IX_Deliveries_EndpointId",
                table: "Deliveries");

            migrationBuilder.DropIndex(
                name: "IX_Deliveries_EventId",
                table: "Deliveries");
        }
    }
}
