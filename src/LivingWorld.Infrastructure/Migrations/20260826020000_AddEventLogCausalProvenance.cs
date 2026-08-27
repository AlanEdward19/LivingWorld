using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LivingWorld.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddEventLogCausalProvenance : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "EventId",
            table: "EventLog",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "CauseEventId",
            table: "EventLog",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SourceSystem",
            table: "EventLog",
            type: "TEXT",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "EventId",
            table: "EventLog");

        migrationBuilder.DropColumn(
            name: "CauseEventId",
            table: "EventLog");

        migrationBuilder.DropColumn(
            name: "SourceSystem",
            table: "EventLog");
    }
}
