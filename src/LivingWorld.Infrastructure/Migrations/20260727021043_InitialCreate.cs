using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LivingWorld.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "EventLog",
            columns: table => new
            {
                BranchId = table.Column<long>(type: "INTEGER", nullable: false),
                Tick = table.Column<long>(type: "INTEGER", nullable: false),
                Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                Kind = table.Column<string>(type: "TEXT", nullable: false),
                Payload = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EventLog", x => new { x.BranchId, x.Tick, x.Sequence });
            });

        migrationBuilder.CreateTable(
            name: "Snapshots",
            columns: table => new
            {
                BranchId = table.Column<long>(type: "INTEGER", nullable: false),
                Tick = table.Column<long>(type: "INTEGER", nullable: false),
                Json = table.Column<string>(type: "TEXT", nullable: false),
                CanonicalHash = table.Column<string>(type: "TEXT", nullable: false),
                VolatileHash = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Snapshots", x => new { x.BranchId, x.Tick });
            });

        migrationBuilder.CreateIndex(
            name: "IX_EventLog_BranchId",
            table: "EventLog",
            column: "BranchId");

        migrationBuilder.CreateIndex(
            name: "IX_Snapshots_BranchId",
            table: "Snapshots",
            column: "BranchId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "EventLog");

        migrationBuilder.DropTable(
            name: "Snapshots");
    }
}
