using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Template.Infrastructure.Persistence;

namespace Template.Migrations.PostgreSql;

[DbContext(typeof(TemplateDbContext))]
public sealed class TemplateDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.12");

        modelBuilder.Entity("Template.Domain.WorkItems.WorkItem", entity =>
        {
            entity.Property<Guid>("Id")
                .HasColumnType("uuid");

            entity.Property<string>("Status")
                .IsConcurrencyToken()
                .IsRequired()
                .HasMaxLength(32)
                .HasColumnType("character varying(32)");

            entity.Property<string>("Title")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("character varying(200)");

            entity.HasKey("Id");
            entity.ToTable("WorkItems");
        });
    }
}
