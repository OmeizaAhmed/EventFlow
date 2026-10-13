using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueApiKeyProjectPrefix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApiKeys_ProjectId",
                table: "ApiKeys");

            migrationBuilder.AlterColumn<string>(
                name: "KeyPrefix",
                table: "ApiKeys",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_ProjectId_KeyPrefix",
                table: "ApiKeys",
                columns: new[] { "ProjectId", "KeyPrefix" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApiKeys_ProjectId_KeyPrefix",
                table: "ApiKeys");

            migrationBuilder.AlterColumn<string>(
                name: "KeyPrefix",
                table: "ApiKeys",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_ProjectId",
                table: "ApiKeys",
                column: "ProjectId");
        }
    }
}
