using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Mostlylucid.Ephemeral.Atoms.Taxonomy.Manifests;

/// <summary>
///     Loads and validates atom and molecule manifests from YAML files.
/// </summary>
public sealed class ManifestLoader
{
    private readonly Dictionary<string, AtomManifest> _atomManifests = new(StringComparer.OrdinalIgnoreCase);
    private readonly IDeserializer _deserializer;
    private readonly Dictionary<string, MoleculeManifest> _moleculeManifests = new(StringComparer.OrdinalIgnoreCase);

    public ManifestLoader()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <summary>
    ///     All loaded atom manifests by name.
    /// </summary>
    public IReadOnlyDictionary<string, AtomManifest> AtomManifests => _atomManifests;

    /// <summary>
    ///     All loaded molecule manifests by name.
    /// </summary>
    public IReadOnlyDictionary<string, MoleculeManifest> MoleculeManifests => _moleculeManifests;

    /// <summary>
    ///     Loads atom manifests from embedded resources in an assembly.
    /// </summary>
    /// <param name="assembly">Assembly containing embedded YAML resources.</param>
    /// <param name="resourcePattern">Regex pattern to match resource names (default: *.atom.yaml).</param>
    public void LoadAtomsFromAssembly(Assembly assembly, string? resourcePattern = null)
    {
        var pattern = resourcePattern ?? @"\.atom\.yaml$";
        var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            if (!regex.IsMatch(resourceName))
                continue;

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
                continue;

            using var reader = new StreamReader(stream);
            var yaml = reader.ReadToEnd();
            var manifest = _deserializer.Deserialize<AtomManifest>(yaml);

            if (!string.IsNullOrWhiteSpace(manifest.Name))
                _atomManifests[manifest.Name] = manifest;
        }
    }

    /// <summary>
    ///     Loads molecule manifests from embedded resources in an assembly.
    /// </summary>
    /// <param name="assembly">Assembly containing embedded YAML resources.</param>
    /// <param name="resourcePattern">Regex pattern to match resource names (default: *.molecule.yaml).</param>
    public void LoadMoleculesFromAssembly(Assembly assembly, string? resourcePattern = null)
    {
        var pattern = resourcePattern ?? @"\.molecule\.yaml$";
        var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            if (!regex.IsMatch(resourceName))
                continue;

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
                continue;

            using var reader = new StreamReader(stream);
            var yaml = reader.ReadToEnd();
            var manifest = _deserializer.Deserialize<MoleculeManifest>(yaml);

            if (!string.IsNullOrWhiteSpace(manifest.Name))
                _moleculeManifests[manifest.Name] = manifest;
        }
    }

    /// <summary>
    ///     Loads atom manifests from a directory.
    /// </summary>
    /// <param name="directory">Directory containing YAML files.</param>
    /// <param name="searchPattern">File search pattern (default: *.atom.yaml).</param>
    public void LoadAtomsFromDirectory(string directory, string searchPattern = "*.atom.yaml")
    {
        if (!Directory.Exists(directory))
            return;

        foreach (var file in Directory.EnumerateFiles(directory, searchPattern, SearchOption.AllDirectories))
        {
            var yaml = File.ReadAllText(file);
            var manifest = _deserializer.Deserialize<AtomManifest>(yaml);

            if (!string.IsNullOrWhiteSpace(manifest.Name))
                _atomManifests[manifest.Name] = manifest;
        }
    }

    /// <summary>
    ///     Loads molecule manifests from a directory.
    /// </summary>
    /// <param name="directory">Directory containing YAML files.</param>
    /// <param name="searchPattern">File search pattern (default: *.molecule.yaml).</param>
    public void LoadMoleculesFromDirectory(string directory, string searchPattern = "*.molecule.yaml")
    {
        if (!Directory.Exists(directory))
            return;

        foreach (var file in Directory.EnumerateFiles(directory, searchPattern, SearchOption.AllDirectories))
        {
            var yaml = File.ReadAllText(file);
            var manifest = _deserializer.Deserialize<MoleculeManifest>(yaml);

            if (!string.IsNullOrWhiteSpace(manifest.Name))
                _moleculeManifests[manifest.Name] = manifest;
        }
    }

    /// <summary>
    ///     Gets an atom manifest by name.
    /// </summary>
    public AtomManifest? GetAtom(string name)
    {
        return _atomManifests.TryGetValue(name, out var manifest) ? manifest : null;
    }

    /// <summary>
    ///     Gets a molecule manifest by name.
    /// </summary>
    public MoleculeManifest? GetMolecule(string name)
    {
        return _moleculeManifests.TryGetValue(name, out var manifest) ? manifest : null;
    }

    /// <summary>
    ///     Gets all atom manifests ordered by priority (descending).
    /// </summary>
    public IReadOnlyList<AtomManifest> GetOrderedAtoms()
    {
        return _atomManifests.Values
            .OrderByDescending(m => m.Lane?.Priority ?? 50)
            .ThenBy(m => m.Name)
            .ToList();
    }

    /// <summary>
    ///     Gets all molecule manifests ordered by priority (descending).
    /// </summary>
    public IReadOnlyList<MoleculeManifest> GetOrderedMolecules()
    {
        return _moleculeManifests.Values
            .OrderByDescending(m => m.Lane?.Priority ?? 50)
            .ThenBy(m => m.Name)
            .ToList();
    }

    /// <summary>
    ///     Builds a dependency graph of atoms based on signal dependencies.
    /// </summary>
    /// <returns>Dictionary mapping atom name to set of dependency atom names.</returns>
    public Dictionary<string, HashSet<string>> BuildAtomDependencyGraph()
    {
        // First pass: map signals to producing atoms
        var signalProducers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, manifest) in _atomManifests)
        {
            foreach (var signal in manifest.Emits.OnComplete)
                if (!string.IsNullOrWhiteSpace(signal.Key))
                    signalProducers[signal.Key] = name;

            foreach (var signal in manifest.Emits.Conditional)
                if (!string.IsNullOrWhiteSpace(signal.Key))
                    signalProducers[signal.Key] = name;
        }

        // Second pass: build dependency graph
        var graph = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, manifest) in _atomManifests)
        {
            var dependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (manifest.Listens is not null)
            {
                foreach (var signal in manifest.Listens.Required)
                    if (signalProducers.TryGetValue(signal, out var producer) && producer != name)
                        dependencies.Add(producer);

                foreach (var signal in manifest.Listens.Optional)
                    if (signalProducers.TryGetValue(signal, out var producer) && producer != name)
                        dependencies.Add(producer);
            }

            graph[name] = dependencies;
        }

        return graph;
    }

    /// <summary>
    ///     Gets atoms that can run given the currently available signals.
    /// </summary>
    /// <param name="availableSignals">Set of currently available signal keys.</param>
    public IReadOnlyList<AtomManifest> GetRunnableAtoms(IReadOnlySet<string> availableSignals)
    {
        return _atomManifests.Values
            .Where(m => CanRun(m, availableSignals))
            .OrderByDescending(m => m.Lane?.Priority ?? 50)
            .ToList();
    }

    /// <summary>
    ///     Checks if an atom can run given available signals.
    /// </summary>
    public bool CanRun(AtomManifest manifest, IReadOnlySet<string> availableSignals)
    {
        // Check skip conditions first
        if (manifest.Triggers?.SkipWhen is not null)
            foreach (var condition in manifest.Triggers.SkipWhen)
                if (EvaluateCondition(condition, availableSignals))
                    return false;

        // Check required conditions
        if (manifest.Triggers?.Requires is not null)
            foreach (var condition in manifest.Triggers.Requires)
                if (!EvaluateCondition(condition, availableSignals))
                    return false;

        // Check required signals in listens
        if (manifest.Listens?.Required is not null)
            foreach (var signal in manifest.Listens.Required)
            {
                var expandedSignal = ExpandSignalTemplate(signal, manifest);
                if (!availableSignals.Contains(expandedSignal))
                    return false;
            }

        return true;
    }

    /// <summary>
    ///     Gets all signals emitted by all loaded atoms.
    /// </summary>
    public IReadOnlySet<string> GetAllEmittedSignals()
    {
        var signals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var manifest in _atomManifests.Values)
        {
            foreach (var signal in manifest.Emits.OnStart)
                signals.Add(ExpandSignalTemplate(signal, manifest));

            foreach (var signal in manifest.Emits.OnComplete)
                if (!string.IsNullOrWhiteSpace(signal.Key))
                    signals.Add(ExpandSignalTemplate(signal.Key, manifest));

            foreach (var signal in manifest.Emits.OnFailure)
                if (!string.IsNullOrWhiteSpace(signal.Key))
                    signals.Add(ExpandSignalTemplate(signal.Key, manifest));

            foreach (var signal in manifest.Emits.Conditional)
                if (!string.IsNullOrWhiteSpace(signal.Key))
                    signals.Add(ExpandSignalTemplate(signal.Key, manifest));
        }

        return signals;
    }

    /// <summary>
    ///     Generates a human/LLM-readable summary of all signal contracts.
    /// </summary>
    public string GetSignalContractsSummary()
    {
        var sb = new StringBuilder();

        foreach (var manifest in GetOrderedAtoms())
        {
            sb.AppendLine(
                $"Atom: {manifest.Name} (kind: {manifest.Taxonomy.Kind}, priority: {manifest.Lane?.Priority ?? 50})");
            sb.AppendLine($"  Scope: {manifest.Scope.Sink}.{manifest.Scope.Coordinator}.{manifest.Scope.Atom}");

            if (manifest.Listens?.Required?.Count > 0)
                sb.AppendLine($"  Requires: {string.Join(", ", manifest.Listens.Required)}");

            if (manifest.Listens?.Optional?.Count > 0)
                sb.AppendLine($"  Uses: {string.Join(", ", manifest.Listens.Optional)}");

            var emits = manifest.Emits.OnComplete.Select(s => s.Key)
                .Concat(manifest.Emits.Conditional.Select(s => s.Key))
                .Where(s => !string.IsNullOrWhiteSpace(s));
            if (emits.Any())
                sb.AppendLine($"  Emits: {string.Join(", ", emits)}");

            if (manifest.Preserve?.Escalate?.Count > 0)
            {
                var escalations = manifest.Preserve.Escalate.Select(e => $"{e.Signal}→{e.To}");
                sb.AppendLine($"  Escalates: {string.Join(", ", escalations)}");
            }

            if (manifest.Tags?.Count > 0)
                sb.AppendLine($"  Tags: {string.Join(", ", manifest.Tags)}");

            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Converts a manifest to an AtomContract for runtime use.
    /// </summary>
    public AtomContract ToContract(AtomManifest manifest)
    {
        var kind = AtomKind.From(manifest.Taxonomy.Kind);

        var determinism = manifest.Taxonomy.Determinism.Equals("probabilistic", StringComparison.OrdinalIgnoreCase)
            ? AtomDeterminism.Probabilistic
            : AtomDeterminism.Deterministic;

        var persistence = manifest.Taxonomy.Persistence.ToLowerInvariant() switch
        {
            "escalatable" => AtomPersistence.PersistableViaEscalation,
            "direct_write" => AtomPersistence.DirectWriteAllowed,
            _ => AtomPersistence.EphemeralOnly
        };

        AtomBudget? budget = null;
        if (manifest.Budget is not null)
        {
            TimeSpan? maxDuration = null;
            if (!string.IsNullOrWhiteSpace(manifest.Budget.MaxDuration) &&
                TimeSpan.TryParse(manifest.Budget.MaxDuration, out var duration))
                maxDuration = duration;

            budget = new AtomBudget(maxDuration, manifest.Budget.MaxTokens, manifest.Budget.MaxCost);
        }

        var reads = manifest.Listens?.Required?.Concat(manifest.Listens?.Optional ?? Enumerable.Empty<string>())
                        .ToList()
                    ?? new List<string>();

        var writes = manifest.Emits.OnComplete.Select(s => s.Key)
            .Concat(manifest.Emits.Conditional.Select(s => s.Key))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList()!;

        return AtomContract.Create(
            kind,
            determinism,
            persistence,
            manifest.Name,
            reads,
            writes,
            budget,
            manifest.Evidence?.Requirements);
    }

    private bool EvaluateCondition(SignalCondition condition, IReadOnlySet<string> availableSignals)
    {
        var signal = condition.Signal;

        // Simple presence check (no condition)
        if (string.IsNullOrWhiteSpace(condition.Condition) && condition.Value is null)
            return availableSignals.Contains(signal);

        // HasValue means signal exists
        if (condition.Condition?.Equals("HasValue", StringComparison.OrdinalIgnoreCase) == true)
            return availableSignals.Contains(signal);

        // IsNullOrWhiteSpace means signal doesn't exist
        if (condition.Condition?.Equals("IsNullOrWhiteSpace", StringComparison.OrdinalIgnoreCase) == true)
            return !availableSignals.Contains(signal);

        // For other conditions, we just check presence (runtime evaluation handles values)
        return availableSignals.Contains(signal);
    }

    private string ExpandSignalTemplate(string template, AtomManifest manifest)
    {
        return template
            .Replace("{scope.sink}", manifest.Scope.Sink, StringComparison.OrdinalIgnoreCase)
            .Replace("{scope.coordinator}", manifest.Scope.Coordinator, StringComparison.OrdinalIgnoreCase)
            .Replace("{scope.atom}", manifest.Scope.Atom, StringComparison.OrdinalIgnoreCase)
            .Replace("{name}", manifest.Name, StringComparison.OrdinalIgnoreCase);
    }
}