using CustomCodeFramework.Postgres.EntityFramework.Configurations;
using Dhole.Notifications.Domain.Notifications.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dhole.Notifications.Persistence.Configurations;

internal sealed class NotificationTemplateConfiguration : EntityTypeConfigurationBase<NotificationTemplate, Guid>
{
    public override void Configure(EntityTypeBuilder<NotificationTemplate> builder)
    {
        base.Configure(builder);
        builder.ToTable("notification_templates");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Code).HasMaxLength(160).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(250).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1500);
        builder.Property(x => x.NotificationType).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Channel).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.SubjectTemplate).HasMaxLength(1000);
        builder.Property(x => x.BodyTemplate).HasColumnType("text").IsRequired();
        builder.Property(x => x.DesignerJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);
        builder.HasIndex(x => new { x.NotificationType, x.Channel, x.IsActive });
    }
}
