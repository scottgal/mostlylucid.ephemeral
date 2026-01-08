using System.Collections.Concurrent;
using System.Reflection;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Mostlylucid.Ephemeral.Atoms.Taxonomy.Manifests;

/// <summary>
///     Registry for discovering and loading atom/molecule manifests at runtime.
///     Supports loading from files, embedded resources, and remote URLs.
/// </summary>
public sealed class ManifestRegistry : IManifestRegistry
{
    private readonly ConcurrentDictionary<string, AtomManifest> _atoms = new(StringComparer.OrdinalIgnoreCase);
    private readonly IDeserializer _deserializer;
    private readonly ConcurrentDictionary<string, MoleculeManifest> _molecules = new(StringComparer.OrdinalIgnoreCase);
    private readonly ManifestRegistryOptions _options;

    public ManifestRegistry(ManifestRegistryOptions? options = null)
    {
        _options = options ?? new ManifestRegistryOptions();
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <summary>
    ///     All registered atom manifests.
    /// </summary>
    public IReadOnlyDictionary<string, AtomManifest> Atoms => _atoms;

    /// <summary>
    ///     All registered molecule manifests.
    /// </summary>
    public IReadOnlyDictionary<string, MoleculeManifest> Molecules => _molecules;

    /// <summary>
    ///     Loads all manifests from a directory.
    /// </summary>
    public async Task LoadFromDirectoryAsync(string path, CancellationToken ct = default)
    {
        if (!Directory.Exists(path))
            return;

        var atomFiles = Directory.EnumerateFiles(path, "*.atom.yaml", SearchOption.AllDirectories);
        var moleculeFiles = Directory.EnumerateFiles(path, "*.molecule.yaml", SearchOption.AllDirectories);

        foreach (var file in atomFiles)
        {
            ct.ThrowIfCancellationRequested();
            var content = await File.ReadAllTextAsync(file, ct);
            var manifest = _deserializer.Deserialize<AtomManifest>(content);
            var key = GetAtomKey(manifest);
            _atoms[key] = manifest;
        }

        foreach (var file in moleculeFiles)
        {
            ct.ThrowIfCancellationRequested();
            var content = await File.ReadAllTextAsync(file, ct);
            var manifest = _deserializer.Deserialize<MoleculeManifest>(content);
            var key = GetMoleculeKey(manifest);
            _molecules[key] = manifest;
        }
    }

    /// <summary>
    ///     Loads manifests from embedded resources in an assembly.
    /// </summary>
    public void LoadFromAssembly(Assembly assembly, string? resourcePrefix = null)
    {
        var resources = assembly.GetManifestResourceNames();

        foreach (var resource in resources)
        {
            if (resourcePrefix != null && !resource.StartsWith(resourcePrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            using var stream = assembly.GetManifestResourceStream(resource);
            if (stream == null)
                continue;

            using var reader = new StreamReader(stream);
            var content = reader.ReadToEnd();

            if (resource.EndsWith(".atom.yaml", StringComparison.OrdinalIgnoreCase))
            {
                var manifest = _deserializer.Deserialize<AtomManifest>(content);
                var key = GetAtomKey(manifest);
                _atoms[key] = manifest;
            }
            else if (resource.EndsWith(".molecule.yaml", StringComparison.OrdinalIgnoreCase))
            {
                var manifest = _deserializer.Deserialize<MoleculeManifest>(content);
                var key = GetMoleculeKey(manifest);
                _molecules[key] = manifest;
            }
        }
    }

    /// <summary>
    ///     Registers an atom manifest directly.
    /// </summary>
    public void Register(AtomManifest manifest)
    {
        var key = GetAtomKey(manifest);
        _atoms[key] = manifest;
    }

    /// <summary>
    ///     Registers a molecule manifest directly.
    /// </summary>
    public void Register(MoleculeManifest manifest)
    {
        var key = GetMoleculeKey(manifest);
        _molecules[key] = manifest;
    }

    /// <summary>
    ///     Gets an atom manifest by its fully qualified key.
    /// </summary>
    public AtomManifest? GetAtom(string key)
    {
        return _atoms.TryGetValue(key, out var manifest) ? manifest : null;
    }

    /// <summary>
    ///     Gets an atom manifest by name (searches all scopes).
    /// </summary>
    public AtomManifest? GetAtomByName(string name)
    {
        return _atoms.Values.FirstOrDefault(m =>
            m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Gets a molecule manifest by its fully qualified key.
    /// </summary>
    public MoleculeManifest? GetMolecule(string key)
    {
        return _molecules.TryGetValue(key, out var manifest) ? manifest : null;
    }

    /// <summary>
    ///     Gets atoms by tag.
    /// </summary>
    public IEnumerable<AtomManifest> GetAtomsByTag(string tag)
    {
        return _atoms.Values.Where(m =>
            m.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Gets atoms by kind.
    /// </summary>
    public IEnumerable<AtomManifest> GetAtomsByKind(string kind)
    {
        return _atoms.Values.Where(m =>
            m.Taxonomy.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Gets atoms in a specific scope (sink.coordinator).
    /// </summary>
    public IEnumerable<AtomManifest> GetAtomsInScope(string sink, string? coordinator = null)
    {
        return _atoms.Values.Where(m =>
        {
            if (!m.Scope.Sink.Equals(sink, StringComparison.OrdinalIgnoreCase))
                return false;

            if (coordinator != null && !m.Scope.Coordinator.Equals(coordinator, StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        });
    }

    /// <summary>
    ///     Finds atoms that emit a specific signal pattern.
    /// </summary>
    public IEnumerable<AtomManifest> FindProvidersOf(string signalPattern)
    {
        return _atoms.Values.Where(m => EmitsSignal(m, signalPattern));
    }

    /// <summary>
    ///     Finds atoms that require a specific signal pattern.
    /// </summary>
    public IEnumerable<AtomManifest> FindConsumersOf(string signalPattern)
    {
        return _atoms.Values.Where(m => RequiresSignal(m, signalPattern));
    }

    /// <summary>
    ///     Validates that all signal dependencies are satisfied within the registry.
    /// </summary>
    public ValidationResult Validate()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        foreach (var atom in _atoms.Values)
        {
            if (atom.Listens?.Required == null)
                continue;

            foreach (var required in atom.Listens.Required)
            {
                var providers = FindProvidersOf(required);
                if (!providers.Any())
                    errors.Add($"Atom '{atom.Name}' requires signal '{required}' but no provider found");
            }
        }

        // Check for circular dependencies
        var graph = BuildDependencyGraph();
        if (HasCycles(graph)) errors.Add("Circular dependency detected in signal graph");

        return new ValidationResult(errors, warnings);
    }

    /// <summary>
    ///     Builds a dependency graph based on signal contracts.
    /// </summary>
    public SignalDependencyGraph BuildDependencyGraph()
    {
        var graph = new SignalDependencyGraph();

        foreach (var atom in _atoms.Values) graph.AddNode(atom);

        foreach (var atom in _atoms.Values)
        {
            if (atom.Listens?.Required == null)
                continue;

            foreach (var required in atom.Listens.Required)
            {
                var providers = FindProvidersOf(required);
                foreach (var provider in providers) graph.AddEdge(provider, atom, required);
            }
        }

        return graph;
    }

    /// <summary>
    ///     Returns atoms in topological order (providers before consumers).
    /// </summary>
    public IReadOnlyList<AtomManifest> TopologicalSort()
    {
        var graph = BuildDependencyGraph();
        return graph.TopologicalSort();
    }

    private static string GetAtomKey(AtomManifest manifest)
    {
        return $"{manifest.Scope.Sink}.{manifest.Scope.Coordinator}.{manifest.Scope.Atom}";
    }

    private static string GetMoleculeKey(MoleculeManifest manifest)
    {
        return $"{manifest.Scope.Sink}.{manifest.Scope.Coordinator}.{manifest.Scope.Molecule}";
    }

    private static bool EmitsSignal(AtomManifest manifest, string pattern)
    {
        var emitted = manifest.Emits.OnComplete
            .Select(s => s.Key)
            .Concat(manifest.Emits.OnStart)
            .Concat(manifest.Emits.Conditional.Select(c => c.Key));

        return emitted.Any(s => MatchesPattern(s, pattern));
    }

    private static bool RequiresSignal(AtomManifest manifest, string pattern)
    {
        if (manifest.Listens?.Required == null)
            return false;

        return manifest.Listens.Required.Any(s => MatchesPattern(s, pattern));
    }

    private static bool MatchesPattern(string signal, string pattern)
    {
        if (pattern == "*")
            return true;

        if (pattern.EndsWith("*"))
        {
            var prefix = pattern[..^1];
            return signal.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        return signal.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasCycles(SignalDependencyGraph graph)
    {
        // Simple cycle detection using DFS
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();

        foreach (var node in graph.Nodes)
            if (HasCyclesDfs(node.Name, graph, visited, recursionStack))
                return true;

        return false;
    }

    private static bool HasCyclesDfs(
        string current,
        SignalDependencyGraph graph,
        HashSet<string> visited,
        HashSet<string> recursionStack)
    {
        if (recursionStack.Contains(current))
            return true;

        if (visited.Contains(current))
            return false;

        visited.Add(current);
        recursionStack.Add(current);

        foreach (var edge in graph.GetOutgoingEdges(current))
            if (HasCyclesDfs(edge.Target.Name, graph, visited, recursionStack))
                return true;

        recursionStack.Remove(current);
        return false;
    }
}

/// <summary>
///     Options for configuring the manifest registry.
/// </summary>
public sealed class ManifestRegistryOptions
{
    /// <summary>
    ///     Whether to auto-discover from loaded assemblies.
    /// </summary>
    public bool AutoDiscover { get; init; } = false;

    /// <summary>
    ///     Directories to scan for manifests.
    /// </summary>
    public List<string> ScanDirectories { get; init; } = new();

    /// <summary>
    ///     NuGet sources for package resolution.
    /// </summary>
    public List<NuGetSource> NuGetSources { get; init; } = new();
}

/// <summary>
///     A NuGet source for package resolution.
/// </summary>
public sealed class NuGetSource
{
    public string Name { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string? ApiKey { get; init; }
}

/// <summary>
///     Result of manifest validation.
/// </summary>
public sealed class ValidationResult
{
    public ValidationResult(List<string> errors, List<string> warnings)
    {
        Errors = errors;
        Warnings = warnings;
    }

    public IReadOnlyList<string> Errors { get; }
    public IReadOnlyList<string> Warnings { get; }
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
///     A dependency graph based on signal contracts.
/// </summary>
public sealed class SignalDependencyGraph
{
    private readonly List<SignalEdge> _edges = new();
    private readonly Dictionary<string, AtomManifest> _nodes = new(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<AtomManifest> Nodes => _nodes.Values;
    public IEnumerable<SignalEdge> Edges => _edges;

    public void AddNode(AtomManifest manifest)
    {
        _nodes[manifest.Name] = manifest;
    }

    public void AddEdge(AtomManifest from, AtomManifest to, string signal)
    {
        _edges.Add(new SignalEdge(from, to, signal));
    }

    public IEnumerable<SignalEdge> GetOutgoingEdges(string atomName)
    {
        return _edges.Where(e => e.Source.Name.Equals(atomName, StringComparison.OrdinalIgnoreCase));
    }

    public IEnumerable<SignalEdge> GetIncomingEdges(string atomName)
    {
        return _edges.Where(e => e.Target.Name.Equals(atomName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Returns nodes in topological order (providers before consumers).
    /// </summary>
    public IReadOnlyList<AtomManifest> TopologicalSort()
    {
        var result = new List<AtomManifest>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var temp = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in _nodes.Values.OrderByDescending(n => GetPriority(n)))
            if (!visited.Contains(node.Name))
                TopologicalSortVisit(node, visited, temp, result);

        result.Reverse();
        return result;
    }

    private void TopologicalSortVisit(
        AtomManifest node,
        HashSet<string> visited,
        HashSet<string> temp,
        List<AtomManifest> result)
    {
        if (temp.Contains(node.Name))
            throw new InvalidOperationException($"Circular dependency detected at '{node.Name}'");

        if (visited.Contains(node.Name))
            return;

        temp.Add(node.Name);

        foreach (var edge in GetOutgoingEdges(node.Name)) TopologicalSortVisit(edge.Target, visited, temp, result);

        temp.Remove(node.Name);
        visited.Add(node.Name);
        result.Add(node);
    }

    private static int GetPriority(AtomManifest manifest)
    {
        return manifest.Lane?.Priority ?? 50;
    }
}

/// <summary>
///     An edge in the dependency graph representing a signal flow.
/// </summary>
public sealed class SignalEdge
{
    public SignalEdge(AtomManifest source, AtomManifest target, string signal)
    {
        Source = source;
        Target = target;
        Signal = signal;
    }

    public AtomManifest Source { get; }
    public AtomManifest Target { get; }
    public string Signal { get; }
}

/// <summary>
///     Interface for manifest registry operations.
/// </summary>
public interface IManifestRegistry
{
    IReadOnlyDictionary<string, AtomManifest> Atoms { get; }
    IReadOnlyDictionary<string, MoleculeManifest> Molecules { get; }

    Task LoadFromDirectoryAsync(string path, CancellationToken ct = default);
    void LoadFromAssembly(Assembly assembly, string? resourcePrefix = null);
    void Register(AtomManifest manifest);
    void Register(MoleculeManifest manifest);

    AtomManifest? GetAtom(string key);
    AtomManifest? GetAtomByName(string name);
    MoleculeManifest? GetMolecule(string key);

    IEnumerable<AtomManifest> GetAtomsByTag(string tag);
    IEnumerable<AtomManifest> GetAtomsByKind(string kind);
    IEnumerable<AtomManifest> GetAtomsInScope(string sink, string? coordinator = null);

    IEnumerable<AtomManifest> FindProvidersOf(string signalPattern);
    IEnumerable<AtomManifest> FindConsumersOf(string signalPattern);

    ValidationResult Validate();
    SignalDependencyGraph BuildDependencyGraph();
    IReadOnlyList<AtomManifest> TopologicalSort();
}