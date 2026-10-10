using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Acme.Infrastructure.Persistence;

namespace Acme.Migrations.SqlServer;

[DbContext(typeof(AcmeDbContext))]
public sealed class AcmeDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.12");

        modelBuilder.Entity("Acme.Domain.WorkItems.WorkItem", entity =>
        {
            entity.Property<Guid>("Id")
                .ValueGeneratedNever()
                .HasColumnType("uniqueidentifier");

            entity.Property<string>("Status")
                .IsConcurrencyToken()
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
