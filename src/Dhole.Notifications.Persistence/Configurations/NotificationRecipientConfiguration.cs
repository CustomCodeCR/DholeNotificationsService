using CustomCodeFramework.Postgres.EntityFramework.Configurations;
using Dhole.Notifications.Domain.Notifications.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dhole.Notifications.Persistence.Configurations;

internal sealed class NotificationRecipientConfiguration : EntityTypeConfigurationBase<NotificationRecipient, Guid>
{
    public override void Configure(EntityTypeBuilder<NotificationRecipient> builder)
    {
        base.Configure(builder);
        builder.ToTable("notification_recipients");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Address).HasMaxLength(500).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(250);
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.ReadAtUtc);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.Address);
        builder.HasIndex(x => new { x.UserId, x.ReadAtUtc, x.CreatedAtUtc })
            .HasDatabaseName("IX_notification_recipients_user_read_created");
    }
}
