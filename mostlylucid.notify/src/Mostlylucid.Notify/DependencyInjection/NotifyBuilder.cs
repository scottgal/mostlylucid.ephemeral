using Microsoft.Extensions.DependencyInjection;
using Mostlylucid.Notify.Email;

namespace Mostlylucid.Notify.DependencyInjection;

/// <summary>
///     Fluent builder for opt-in notify configuration. Each method is an explicit registration
///     so the trimmer keeps only what is wired -- no reflection-based scanning.
/// </summary>
public sealed class NotifyBuilder
{
    public IServiceCollection Services { get; }

    internal NotifyBuilder(IServiceCollection services)
    {
        Services = services;
    }

    /// <summary>
    ///     Register the production email sender (SmtpEmailTransport over SmtpAtom + MailKit).
    /// </summary>
    public NotifyBuilder AddNotifyEmail()
    {
        Services.AddSingleton<IEmailTransport, SmtpEmailTransport>();
        Services.AddSingleton<INotificationSender, EmailSender>();
        return this;
    }

    /// <summary>
    ///     Register the dev/no-smtp logging sender. Replaces any previously registered
    ///     <see cref="INotificationSender"/>.
    /// </summary>
    public NotifyBuilder AddNotifyEmailLogging()
    {
        Services.AddSingleton<INotificationSender, LoggingEmailSender>();
        return this;
    }

    /// <summary>
    ///     Register a template implementation under a string key. The trimmer keeps both types
    ///     because they are explicitly referenced.
    /// </summary>
    public NotifyBuilder AddEmailTemplate<TModel, TTemplate>(string key)
        where TTemplate : class, INotificationTemplate<TModel>
    {
        Services.AddSingleton<INotificationTemplate<TModel>, TTemplate>();
        Services.AddSingleton(sp =>
        {
            var registry = sp.GetRequiredService<EmailTemplateRegistry>();
            registry.Register(key, sp.GetRequiredService<INotificationTemplate<TModel>>());
            return new TemplateRegistration(key);
        });
        return this;
    }

    /// <summary>Marker so the eager registry-populating singleton above is realized at startup.</summary>
    public sealed record TemplateRegistration(string Key);
}
