using CustomCodeFramework.Postgres.EntityFramework.Configurations;
using Dhole.Notifications.Domain.Notifications.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dhole.Notifications.Persistence.Configurations;

internal sealed class NotificationDeliveryAttemptConfiguration : EntityTypeConfigurationBase<NotificationDeliveryAttempt, Guid>
{
    public override void Configure(EntityTypeBuilder<NotificationDeliveryAttempt> builder)
    {
        base.Configure(builder);
        builder.ToTable("notification_delivery_attempts");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Provider).HasMaxLength(160);
        builder.Property(x => x.ProviderMessageId).HasMaxLength(500);
        builder.Property(x => x.ErrorCode).HasMaxLength(160);
        builder.Property(x => x.ErrorMessage).HasMaxLength(4000);
        builder.HasIndex(x => new { x.NotificationMessageId, x.NotificationRecipientId, x.AttemptNumber });
    }
}
