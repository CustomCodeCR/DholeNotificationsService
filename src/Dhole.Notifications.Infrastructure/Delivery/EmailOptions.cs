namespace Dhole.Notifications.Infrastructure.Delivery;

public sealed class EmailOptions
{
    public const string SectionName = "Notifications:Email";
    public bool Enabled { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "Dhole";
}
