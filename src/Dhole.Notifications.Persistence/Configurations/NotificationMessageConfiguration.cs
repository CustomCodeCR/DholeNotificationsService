using CustomCodeFramework.Postgres.EntityFramework.Configurations;
using Dhole.Notifications.Domain.Notifications.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dhole.Notifications.Persistence.Configurations;

internal sealed class NotificationMessageConfiguration : EntityTypeConfigurationBase<NotificationMessage, Guid>
{
    public override void Configure(EntityTypeBuilder<NotificationMessage> builder)
    {
        base.Configure(builder);
        builder.ToTable("notification_messages");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.NotificationType).HasMaxLength(160).IsRequired();
        builder.Property(x => x.TemplateCode).HasMaxLength(160);
        builder.Property(x => x.Channel).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(160);
        builder.Property(x => x.EntityId).HasMaxLength(200);
        builder.Property(x => x.Subject).HasMaxLength(1000);
        builder.Property(x => x.Body).HasColumnType("text");
        builder.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.LastErrorCode).HasMaxLength(160);
        builder.Property(x => x.LastErrorMessage).HasMaxLength(4000);
        builder.HasIndex(x => new { x.Status, x.ScheduledForUtc, x.NextAttemptAtUtc });
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
        builder.HasIndex(x => x.NotificationType);
        builder.HasMany(x => x.Recipients).WithOne(x => x.NotificationMessage)
            .HasForeignKey(x => x.NotificationMessageId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Recipients).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(x => x.DeliveryAttempts).WithOne()
            .HasForeignKey(x => x.NotificationMessageId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.DeliveryAttempts).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
