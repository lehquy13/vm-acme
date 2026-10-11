using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VmAcme.Persistence;

namespace VmAcme.Migrations.PostgreSql;

[DbContext(typeof(VmAcmeDbContext))]
[Migration("20261010000100_InitialCreate")]
public sealed class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "WorkItems",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_WorkItems", workItem => workItem.Id));
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "WorkItems");
}