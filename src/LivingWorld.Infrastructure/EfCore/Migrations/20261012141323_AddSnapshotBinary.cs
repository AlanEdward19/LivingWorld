using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LivingWorld.Infrastructure.EfCore.Migrations;

/// <inheritdoc />
public partial class AddSnapshotBinary : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "BaseTick",
            table: "Snapshots",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<byte[]>(
            name: "Binary",
            table: "Snapshots",
            type: "BLOB",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "BaseTick",
            table: "Snapshots");

        migrationBuilder.DropColumn(
            name: "Binary",
            table: "Snapshots");
    }
}
