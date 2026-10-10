using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Template.Infrastructure.Persistence;

namespace Template.Migrations.SqlServer;

[DbContext(typeof(TemplateDbContext))]
public sealed class TemplateDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.12");

        modelBuilder.Entity("Template.Domain.WorkItems.WorkItem", entity =>
        {
            entity.Property<Guid>("Id")
                .ValueGeneratedNever()
                .HasColumnType("uniqueidentifier");

            entity.Property<string>("Status")
                .IsRequired()
                .HasMaxLength(32)
                .HasColumnType("nvarchar(32)");

            entity.Property<string>("Title")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("nvarchar(200)");

            entity.HasKey("Id");
            entity.ToTable("WorkItems");
        });
    }
}
