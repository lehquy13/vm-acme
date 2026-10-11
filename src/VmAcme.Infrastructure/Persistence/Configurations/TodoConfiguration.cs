using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VmAcme.Todos;
using VmAcme.Users;

namespace VmAcme.Persistence.Configurations;

public sealed class TodoConfiguration : IEntityTypeConfiguration<TodoItem>
{
    public void Configure(EntityTypeBuilder<TodoItem> builder)
    {
        builder.ToTable("TodoItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Description).HasMaxLength(255).IsRequired();
        builder.Property(x => x.Priority).HasConversion<int>();
        builder.Property(x => x.IsCompleted).IsConcurrencyToken();
        builder.Property(x => x.DueDate).HasConversion(x => x.HasValue ? x.Value.ToUniversalTime() : x, x => x);
        builder.Property(x => x.CreatedAt).HasConversion(x => x.ToUniversalTime(), x => x);
        builder.Property(x => x.CompletedAt).HasConversion(x => x.HasValue ? x.Value.ToUniversalTime() : x, x => x);
        builder.Property(x => x.Labels).HasConversion(
                x => JsonSerializer.Serialize(x, (JsonSerializerOptions?)null),
                x => JsonSerializer.Deserialize<string[]>(x, (JsonSerializerOptions?)null) ?? Array.Empty<string>())
            .Metadata.SetValueComparer(new ValueComparer<string[]>(
                (a, b) => a != null && b != null && a.SequenceEqual(b),
                x => x.Aggregate(0, (hash, label) => HashCode.Combine(hash, label.GetHashCode())), x => x.ToArray()));
        builder.HasIndex(x => x.UserId);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}