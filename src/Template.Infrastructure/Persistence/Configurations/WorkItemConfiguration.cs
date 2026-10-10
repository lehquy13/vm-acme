using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Template.Domain.WorkItems;

namespace Template.Infrastructure.Persistence.Configurations;

public sealed class WorkItemConfiguration : IEntityTypeConfiguration<WorkItem>
{
    public void Configure(EntityTypeBuilder<WorkItem> builder)
    {
        builder.ToTable("WorkItems");
        builder.HasKey(workItem => workItem.Id);

        builder.Property(workItem => workItem.Id)
            .HasConversion(id => id.Value, value => new WorkItemId(value))
            .ValueGeneratedNever();

        builder.Property(workItem => workItem.Title)
            .HasConversion(title => title.Value, value => new WorkItemTitle(value))
            .HasMaxLength(WorkItemTitle.MaxLength)
            .IsRequired();

        builder.Property(workItem => workItem.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsConcurrencyToken()
            .IsRequired();
    }
}
