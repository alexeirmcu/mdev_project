using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartTripPlanner.Infrastructure.Outbox;

namespace SmartTripPlanner.Infrastructure.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id)
            .ValueGeneratedNever();

        builder.Property(m => m.PlaceProviderReferenceId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(m => m.PayloadJson)
            .HasColumnType("text");

        builder.Property(m => m.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(m => m.RetryCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(m => m.MaxRetries)
            .IsRequired()
            .HasDefaultValue(3);

        builder.Property(m => m.CreatedAt)
            .IsRequired();

        builder.Property(m => m.UpdatedAt)
            .IsRequired();

        builder.Property(m => m.Error)
            .HasMaxLength(2000);

        builder.HasIndex(m => new { m.Status, m.NextAttemptAt, m.CreatedAt })
            .HasDatabaseName("IX_OutboxMessages_Status_NextAttemptAt_CreatedAt");
    }
}
