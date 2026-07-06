namespace Mostlylucid.Ephemeral.Atoms.Llm;

/// <summary>
///     Caller-owned prompt assembler. Pure template against the item's own
///     state — synchronous and side-effect free. Per-caller, never shared.
/// </summary>
public interface IEphemeralPrompter<in TItem>
{
    EphemeralPrompt Build(TItem item);
}
