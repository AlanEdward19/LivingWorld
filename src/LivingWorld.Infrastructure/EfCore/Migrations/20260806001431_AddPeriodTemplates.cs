using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LivingWorld.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddPeriodTemplates : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PeriodTemplates",
            columns: table => new
            {
                PeriodId = table.Column<string>(type: "TEXT", nullable: false),
                Version = table.Column<int>(type: "INTEGER", nullable: false),
                PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                Source = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PeriodTemplates", x => new { x.PeriodId, x.Version });
            });

        migrationBuilder.CreateIndex(
            name: "IX_PeriodTemplates_PeriodId",
            table: "PeriodTemplates",
            column: "PeriodId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "PeriodTemplates");
    }
}
