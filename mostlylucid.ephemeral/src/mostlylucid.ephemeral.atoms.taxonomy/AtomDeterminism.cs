namespace Mostlylucid.Ephemeral.Atoms.Taxonomy;

/// <summary>
/// Indicates whether an atom produces deterministic or probabilistic outputs.
/// </summary>
public enum AtomDeterminism
{
    /// <summary>
    /// Outputs are deterministic for the same inputs.
    /// </summary>
    Deterministic,
    /// <summary>
    /// Outputs are probabilistic and must be treated as proposals.
    /// </summary>
    Probabilistic
}
