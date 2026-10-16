using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LivingWorld.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddFactLog : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "FactLog",
            columns: table => new
            {
                BranchId = table.Column<long>(type: "INTEGER", nullable: false),
                FactId = table.Column<long>(type: "INTEGER", nullable: false),
                Tick = table.Column<long>(type: "INTEGER", nullable: false),
                Kind = table.Column<string>(type: "TEXT", nullable: false),
                Participants = table.Column<string>(type: "TEXT", nullable: false),
                LocationCityId = table.Column<string>(type: "TEXT", nullable: true),
                Significance = table.Column<double>(type: "REAL", nullable: false),
                Payload = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FactLog", x => new { x.BranchId, x.FactId });
            });

        migrationBuilder.CreateIndex(
            name: "IX_FactLog_BranchId",
            table: "FactLog",
            column: "BranchId");

        migrationBuilder.Sql("""
            CREATE TRIGGER fact_log_no_update
            BEFORE UPDATE ON FactLog
            BEGIN
                SELECT RAISE(ABORT, 'FactLog is append-only');
            END;
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER fact_log_no_delete
            BEFORE DELETE ON FactLog
            BEGIN
                SELECT RAISE(ABORT, 'FactLog is append-only');
            END;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS fact_log_no_delete;");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS fact_log_no_update;");
        migrationBuilder.DropTable(name: "FactLog");
    }
}
