using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EventFlow.Infrastructure.Persistence.Migrations;

[DbContext(typeof(EventFlowDbContext))]
[Migration("20261127120000_EnsureWebhookEndpointDescription")]
public sealed class EnsureWebhookEndpointDescription : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "WebhookEndpoints"
            ADD COLUMN IF NOT EXISTS "Description" character varying(1024) NOT NULL DEFAULT '';
            ALTER TABLE "WebhookEndpoints" ALTER COLUMN "Description" DROP DEFAULT;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The previous model already includes this column. Keep any existing descriptions.
    }
}
