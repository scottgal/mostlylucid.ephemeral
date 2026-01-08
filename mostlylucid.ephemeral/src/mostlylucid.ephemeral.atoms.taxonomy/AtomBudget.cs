namespace Mostlylucid.Ephemeral.Atoms.Taxonomy;

/// <summary>
///     Defines budget limits for an atom execution.
/// </summary>
/// <param name="MaxDuration">Maximum runtime allowed for a single execution.</param>
/// <param name="MaxTokens">Maximum token budget for LLM-backed work.</param>
/// <param name="MaxCost">Maximum cost budget for external services.</param>
public sealed record AtomBudget(
    TimeSpan? MaxDuration = null,
    int? MaxTokens = null,
    decimal? MaxCost = null);