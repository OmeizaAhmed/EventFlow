using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DurableDeliveryProcessing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DeliveryAttempts_DeliveryId",
                table: "DeliveryAttempts");

            migrationBuilder.AddColumn<long>(
                name: "DurationMilliseconds",
                table: "DeliveryAttempts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "ResponseBody",
                table: "DeliveryAttempts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LeaseExpiresAt",
                table: "Deliveries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LeaseToken",
                table: "Deliveries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryAttempts_DeliveryId_AttemptNumber",
                table: "DeliveryAttempts",
                columns: new[] { "DeliveryId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_Status_LeaseExpiresAt",
                table: "Deliveries",
                columns: new[] { "Status", "LeaseExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_Status_NextRetryAt_CreatedAt",
                table: "Deliveries",
                columns: new[] { "Status", "NextRetryAt", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DeliveryAttempts_DeliveryId_AttemptNumber",
                table: "DeliveryAttempts");

            migrationBuilder.DropIndex(
                name: "IX_Deliveries_Status_LeaseExpiresAt",
                table: "Deliveries");

            migrationBuilder.DropIndex(
                name: "IX_Deliveries_Status_NextRetryAt_CreatedAt",
                table: "Deliveries");

            migrationBuilder.DropColumn(
                name: "DurationMilliseconds",
                table: "DeliveryAttempts");

            migrationBuilder.DropColumn(
                name: "ResponseBody",
                table: "DeliveryAttempts");

            migrationBuilder.DropColumn(
                name: "LeaseExpiresAt",
                table: "Deliveries");

            migrationBuilder.DropColumn(
                name: "LeaseToken",
                table: "Deliveries");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryAttempts_DeliveryId",
                table: "DeliveryAttempts",
                column: "DeliveryId");
        }
    }
}
