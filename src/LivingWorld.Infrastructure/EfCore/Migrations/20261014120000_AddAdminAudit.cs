using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LivingWorld.Infrastructure.EfCore.Migrations;

/// <inheritdoc />
public partial class AddAdminAudit : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AdminAudit",
            columns: table => new
            {
                BranchId = table.Column<long>(type: "INTEGER", nullable: false),
                Sequence = table.Column<long>(type: "INTEGER", nullable: false),
                Tick = table.Column<long>(type: "INTEGER", nullable: false),
                Actor = table.Column<string>(type: "TEXT", nullable: false),
                Action = table.Column<string>(type: "TEXT", nullable: false),
                Target = table.Column<string>(type: "TEXT", nullable: false),
                Detail = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AdminAudit", x => new { x.BranchId, x.Sequence });
            });

        migrationBuilder.CreateIndex(
            name: "IX_AdminAudit_BranchId",
            table: "AdminAudit",
            column: "BranchId");

        migrationBuilder.Sql("""
            CREATE TRIGGER admin_audit_no_update
            BEFORE UPDATE ON AdminAudit
            BEGIN
                SELECT RAISE(ABORT, 'AdminAudit is append-only');
            END;
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER admin_audit_no_delete
            BEFORE DELETE ON AdminAudit
            BEGIN
                SELECT RAISE(ABORT, 'AdminAudit is append-only');
            END;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS admin_audit_no_delete;");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS admin_audit_no_update;");
        migrationBuilder.DropTable(name: "AdminAudit");
    }
}
