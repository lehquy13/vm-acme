using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VmAcme.Persistence;

namespace VmAcme;

[DbContext(typeof(VmAcmeDbContext))]
[Migration("20261011000100_AddDeploymentRevision")]
public sealed class AddDeploymentRevision : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "DeploymentRevision",
            table: "WorkItems",
            type: "character varying(32)",
            maxLength: 32,
            nullable: true);

        migrationBuilder.Sql("UPDATE \"WorkItems\" SET \"DeploymentRevision\" = 'v2'");

        migrationBuilder.AlterColumn<string>(
            name: "DeploymentRevision",
            table: "WorkItems",
            type: "character varying(32)",
            maxLength: 32,
            nullable: false,
            defaultValue: "v2",
            oldClrType: typeof(string),
            oldType: "character varying(32)",
            oldMaxLength: 32,
            oldNullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "DeploymentRevision", table: "WorkItems");
}