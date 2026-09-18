namespace Dhole.Notifications.Infrastructure.Delivery;

public sealed class AccessCredentialsEmailOptions
{
    public const string SectionName = "Notifications:AccessCredentials";

    public string AccessUrl { get; set; } = "https://sistema.logisticacastrofallas.com";
    public string SupportEmail { get; set; } = "mlang@castrofallas.com";
    public string Subject { get; set; } = "Credenciales de acceso al sistema";
}
