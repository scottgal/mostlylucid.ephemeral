namespace Mostlylucid.Ephemeral.Atoms.Taxonomy;

/// <summary>
/// Describes an atom's authority to persist outputs.
/// </summary>
public enum AtomPersistence
{
    /// <summary>
    /// Outputs are ephemeral and should not be persisted directly.
    /// </summary>
    EphemeralOnly,
    /// <summary>
    /// Outputs may be promoted by an EscalatorAtom.
    /// </summary>
    PersistableViaEscalation,
    /// <summary>
    /// Outputs may be written directly to durable stores.
    /// </summary>
    DirectWriteAllowed
}
