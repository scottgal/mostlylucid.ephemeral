namespace Mostlylucid.Notify.Email;

/// <summary>Bound from <c>Notify:Email</c>. Smtp options live nested under <c>Notify:Email:Smtp</c>.</summary>
public sealed class EmailNotifyOptions
{
    public required string From { get; set; }
    public string? FromName { get; set; }
    public required SmtpOptions Smtp { get; set; }
}

/// <summary>
///     Bindable SMTP settings. We map across to <c>Mostlylucid.Ephemeral.Atoms.Smtp.SmtpOptions</c>
///     at send time inside <c>SmtpEmailTransport</c>.
/// </summary>
public sealed class SmtpOptions
{
    public required string Host { get; set; }
    public int Port { get; set; } = 587;
    public string? User { get; set; }
    public string? Password { get; set; }
    public bool UseTls { get; set; } = true;
}
