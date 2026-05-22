namespace Mostlylucid.Notify.Email;

/// <summary>
///     An email-channel recipient. Currently a single address; extend to Cc/Bcc lists when needed.
/// </summary>
public sealed record EmailRecipient(string Address) : Recipient;
