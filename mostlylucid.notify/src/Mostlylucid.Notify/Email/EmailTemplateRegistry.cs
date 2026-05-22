using System.Collections.Concurrent;

namespace Mostlylucid.Notify.Email;

/// <summary>
///     Maps a string template key to its compile-time template implementation.
///     Explicit registration (no scanning) so the trimmer keeps only what is wired.
/// </summary>
public sealed class EmailTemplateRegistry
{
    private readonly ConcurrentDictionary<string, IRegisteredTemplate> _byKey = new();

    public void Register<TModel>(string key, INotificationTemplate<TModel> template)
    {
        _byKey[key] = new RegisteredTemplate<TModel>(template);
    }

    public bool TryGet(string key, out IRegisteredTemplate template) =>
        _byKey.TryGetValue(key, out template!);

    public interface IRegisteredTemplate
    {
        string Subject(object model);
        Task<string> RenderHtmlAsync(object model, CancellationToken ct);
        Task<string> RenderTextAsync(object model, CancellationToken ct);
    }

    private sealed class RegisteredTemplate<TModel>(INotificationTemplate<TModel> inner) : IRegisteredTemplate
    {
        public string Subject(object model) => inner.Subject((TModel)model);
        public Task<string> RenderHtmlAsync(object model, CancellationToken ct) => inner.RenderHtmlAsync((TModel)model, ct);
        public Task<string> RenderTextAsync(object model, CancellationToken ct) => inner.RenderTextAsync((TModel)model, ct);
    }
}
