using Dhole.Notifications.Domain.Notifications.Entities;
using Dhole.Notifications.Domain.Notifications.Enums;

namespace Dhole.Notifications.UnitTests;
[TestClass]
public sealed class CrmReminderIdempotencyTests
{
    [TestMethod]
    public void Crm_reminder_has_a_stable_durable_notification_primary_key()
    {
        var id=Guid.NewGuid();
        var one=NotificationMessage.Create("crm.followup.reminder.v1",null,
            NotificationChannel.System,"Followup",Guid.NewGuid().ToString(),
            "Pending followup","Review the CRM followup","{}",null,3,null,id);
        var retry=NotificationMessage.Create("crm.followup.reminder.v1",null,
            NotificationChannel.System,"Followup",Guid.NewGuid().ToString(),
            "Pending followup","Review the CRM followup","{}",null,3,null,id);
        Assert.AreEqual(id,one.Id);
        Assert.AreEqual(one.Id,retry.Id);
    }
    [TestMethod]
    public void Existing_notification_workflows_still_generate_independent_ids()
    {
        var a=NotificationMessage.Create("existing",null,NotificationChannel.System,
            null,null,"title","body","{}",null,3);
        var b=NotificationMessage.Create("existing",null,NotificationChannel.System,
            null,null,"title","body","{}",null,3);
        Assert.AreNotEqual(a.Id,b.Id);
    }
}
