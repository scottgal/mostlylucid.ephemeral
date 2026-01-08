# Manifest Packaging System

## Overview

YAML manifests are the primary artifact that define atom/molecule contracts. They reference NuGet packages for implementations. This inverts the typical model:

```
Traditional: Package contains manifests
    NuGet Package → embeds manifests

Inverted: Manifests reference packages
    YAML Manifest → references NuGet Package → contains implementation
```

Benefits of this approach:
- Manifests are human-readable and easily diffable in git
- Can update manifest behavior without rebuilding packages
- Same package can serve multiple manifests
- Manifests can be shared/evolved independently of code

## Manifest Structure with Package Reference

```yaml
# useragent-sensor.atom.yaml
name: "UserAgentSensor"
version: "1.0.0"
description: "Parses and analyzes User-Agent strings"

# Implementation reference
implementation:
  nuget:
    package: "Mostlylucid.BotDetection.Detectors"
    version: ">=2.0.0 <3.0.0"  # SemVer range
    type: "Mostlylucid.BotDetection.Detectors.UserAgentSensor"
    # Optional: specific method if not implementing IAtom
    method: "ExecuteAsync"

taxonomy:
  kind: sensor
  determinism: deterministic
  persistence: ephemeral

# ... rest of manifest
```

## Package Reference Patterns

### 1. Direct Type Reference
```yaml
implementation:
  nuget:
    package: "Mostlylucid.BotDetection.Detectors"
    version: "^2.0.0"
    type: "Mostlylucid.BotDetection.Detectors.UserAgentSensor"
```

### 2. Interface-Based Discovery
```yaml
implementation:
  nuget:
    package: "Mostlylucid.BotDetection.Detectors"
    version: "^2.0.0"
    implements: "ISignalSource"
    name: "UserAgentSensor"  # Discovered by attribute/convention
```

### 3. Factory Pattern
```yaml
implementation:
  nuget:
    package: "Mostlylucid.BotDetection.Detectors"
    version: "^2.0.0"
    factory: "Mostlylucid.BotDetection.DetectorFactory"
    method: "CreateUserAgentSensor"
```

### 4. Generic/Parameterized
```yaml
implementation:
  nuget:
    package: "Mostlylucid.Common.Atoms"
    version: "^1.0.0"
    type: "Mostlylucid.Common.Atoms.HttpSensor<TRequest>"
    type_args:
      TRequest: "Mostlylucid.BotDetection.BotRequest"
```

### 5. No Implementation (Contract Only)
```yaml
# Used for signal contracts without built-in implementation
implementation: null
# Or
implementation:
  mode: contract_only
  note: "Implementations provided by consuming project"
```

## Manifest Distribution

### Option 1: Git Repository
Simplest approach - manifests in a git repo:

```
mostlylucid-atoms/
├── botdetection/
│   ├── atoms/
│   │   ├── useragent-sensor.atom.yaml
│   │   ├── header-sensor.atom.yaml
│   │   └── ...
│   └── molecules/
│       ├── fast-path-pipeline.molecule.yaml
│       └── ...
├── docsummarizer/
│   ├── atoms/
│   └── molecules/
└── manifest.lock  # Version lock file
```

Consume via git submodule or subtree:
```bash
git submodule add https://github.com/scottgal/mostlylucid-atoms.git atoms
```

### Option 2: Simple HTTP Registry
Static file hosting with index:

```json
// https://atoms.mostlylucid.net/index.json
{
  "version": "1.0",
  "atoms": {
    "botdetection.detection.useragent": {
      "versions": {
        "1.0.0": "botdetection/atoms/useragent-sensor.atom.yaml",
        "1.1.0": "botdetection/atoms/useragent-sensor.atom.yaml"
      },
      "latest": "1.1.0"
    }
  },
  "molecules": {
    "botdetection.detection.fast_path": {
      "versions": {
        "1.0.0": "botdetection/molecules/fast-path-pipeline.molecule.yaml"
      },
      "latest": "1.0.0"
    }
  }
}
```

### Option 3: Manifest NuGet Package (Lightweight)
Package containing ONLY manifests (no code):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <PackageId>Mostlylucid.BotDetection.Manifests</PackageId>
    <Version>1.0.0</Version>
    <IncludeBuildOutput>false</IncludeBuildOutput>
  </PropertyGroup>

  <!-- Just manifests, no code -->
  <ItemGroup>
    <Content Include="atoms/**/*.yaml" Pack="true" PackagePath="content/atoms/" />
    <Content Include="molecules/**/*.yaml" Pack="true" PackagePath="content/molecules/" />
  </ItemGroup>

  <!-- No dependencies on implementation packages -->
</Project>
```

## Manifest Signature

Each manifest has a unique signature:

```
<Registry>/<Domain>/<Type>/<Name>@<Version>
```

Examples:
```
mostlylucid.net/botdetection/atoms/UserAgentSensor@1.0.0
github.com/scottgal/botdetection/molecules/FastPathPipeline@2.0.0-beta
local/my-project/atoms/CustomDetector@0.1.0
```

### Signature in Manifest

```yaml
# Full identity
signature:
  registry: "mostlylucid.net"
  domain: "botdetection"
  kind: "atom"
  name: "UserAgentSensor"
  version: "1.0.0"
  checksum: "sha256:abc123..."  # Content hash

# Short form (auto-resolved from path + version)
name: "UserAgentSensor"
version: "1.0.0"
```

## Implementation Package Structure

The NuGet packages that manifests reference:

```
Mostlylucid.BotDetection.Detectors.2.0.0.nupkg
├── lib/
│   └── net10.0/
│       └── Mostlylucid.BotDetection.Detectors.dll
└── (no manifests - they're separate)
```

### Implementation Pattern

```csharp
// In the NuGet package
namespace Mostlylucid.BotDetection.Detectors;

// Marker attribute for discovery
[AtomImplementation("UserAgentSensor")]
public class UserAgentSensor : ISignalSource
{
    public async Task<SignalResult> ExecuteAsync(
        SignalContext context,
        CancellationToken ct)
    {
        // Implementation
    }
}
```

## Runtime Resolution

### Manifest Loader

```csharp
public interface IManifestLoader
{
    // Load from various sources
    Task<AtomManifest> LoadFromFileAsync(string path);
    Task<AtomManifest> LoadFromUrlAsync(string url);
    Task<AtomManifest> LoadFromRegistryAsync(string signature);

    // Resolve implementation
    Task<ISignalSource> ResolveImplementationAsync(AtomManifest manifest);
}

public class ManifestLoader : IManifestLoader
{
    private readonly INuGetResolver _nuget;

    public async Task<ISignalSource> ResolveImplementationAsync(AtomManifest manifest)
    {
        if (manifest.Implementation is null)
            throw new InvalidOperationException("Manifest has no implementation");

        var impl = manifest.Implementation.NuGet;

        // Ensure package is available
        var assembly = await _nuget.LoadPackageAsync(
            impl.Package,
            impl.Version);

        // Resolve type
        var type = assembly.GetType(impl.Type)
            ?? throw new TypeNotFoundException(impl.Type);

        // Create instance via DI
        return (ISignalSource)ActivatorUtilities.CreateInstance(_services, type);
    }
}
```

### NuGet Resolver

```csharp
public interface INuGetResolver
{
    Task<Assembly> LoadPackageAsync(string packageId, string versionRange);
    bool IsPackageAvailable(string packageId, string versionRange);
    Task PreloadPackagesAsync(IEnumerable<NuGetReference> packages);
}

public class NuGetResolver : INuGetResolver
{
    private readonly ILogger<NuGetResolver> _logger;
    private readonly NuGetSettings _settings;

    public async Task<Assembly> LoadPackageAsync(string packageId, string versionRange)
    {
        // 1. Check if already loaded
        var existing = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == packageId);
        if (existing != null)
            return existing;

        // 2. Resolve from NuGet cache or download
        var packagePath = await ResolvePackagePathAsync(packageId, versionRange);

        // 3. Load assembly
        var dllPath = Path.Combine(packagePath, "lib", "net10.0", $"{packageId}.dll");
        return Assembly.LoadFrom(dllPath);
    }
}
```

## Lock File

Like package-lock.json, a manifest.lock file pins exact versions:

```yaml
# manifest.lock
version: 1
locked_at: "2025-01-08T10:30:00Z"

manifests:
  "botdetection/atoms/useragent-sensor.atom.yaml":
    checksum: "sha256:abc123"
    version: "1.0.0"
    implementation:
      package: "Mostlylucid.BotDetection.Detectors"
      resolved_version: "2.1.3"  # Exact version resolved

  "botdetection/atoms/header-sensor.atom.yaml":
    checksum: "sha256:def456"
    version: "1.0.0"
    implementation:
      package: "Mostlylucid.BotDetection.Detectors"
      resolved_version: "2.1.3"

packages:
  "Mostlylucid.BotDetection.Detectors@2.1.3":
    source: "https://api.nuget.org/v3/index.json"
    checksum: "sha512:xyz789"
    dependencies:
      - "Mostlylucid.Ephemeral@2.0.0"
```

## DI Registration

```csharp
services.AddEphemeralAtoms(options =>
{
    // Load manifests from directory
    options.LoadManifestsFrom("./atoms");

    // Configure NuGet sources
    options.NuGet.AddSource("nuget.org", "https://api.nuget.org/v3/index.json");
    options.NuGet.AddSource("mostlylucid", "https://atoms.mostlylucid.net/packages/v3/index.json");

    // Preload packages at startup (optional but faster)
    options.PreloadPackages = true;

    // Use lock file for reproducible builds
    options.UseLockFile("manifest.lock");
});
```

## Workflow

### Development
```bash
# 1. Create/edit manifest
vim atoms/my-sensor.atom.yaml

# 2. Reference implementation package
#    implementation:
#      nuget:
#        package: "MyCompany.Detectors"
#        version: "^1.0.0"
#        type: "MyCompany.Detectors.MySensor"

# 3. Build & run - manifest loader resolves package
dotnet run
```

### Publishing Implementation
```bash
# Build the implementation package (no manifests)
dotnet pack src/MyCompany.Detectors -c Release

# Publish to your registry
dotnet nuget push bin/Release/MyCompany.Detectors.1.0.0.nupkg \
  -s https://api.nuget.org/v3/index.json
```

### Sharing Manifests
```bash
# Commit manifests to git
git add atoms/ molecules/
git commit -m "Add new sensor manifests"
git push

# Others can clone/submodule the manifests
# And reference same implementation packages
```

## Version Resolution

### SemVer Ranges in Manifests

```yaml
implementation:
  nuget:
    package: "Mostlylucid.BotDetection.Detectors"
    version: "^2.0.0"  # Compatible with 2.x
    # Or
    version: ">=2.0.0 <3.0.0"  # Explicit range
    # Or
    version: "2.1.3"  # Exact version
```

### Resolution Strategy

1. **Development**: Use ranges, resolve latest compatible
2. **CI/CD**: Generate lock file with exact versions
3. **Production**: Use lock file for reproducibility

## Self-Hosted Registry

For your own manifests and packages:

### Manifest Registry (Static Files)
```nginx
# nginx config for atoms.mostlylucid.net
server {
    listen 443 ssl;
    server_name atoms.mostlylucid.net;

    root /var/www/atoms;

    location / {
        try_files $uri $uri/ =404;
        add_header Content-Type application/x-yaml;
    }
}
```

### Package Registry (BaGet)
```bash
docker run -d \
  -p 5000:80 \
  -v /var/packages:/var/baget/packages \
  loicsharma/baget
```

### Combined Setup
```
atoms.mostlylucid.net/          # Manifest files (YAML)
├── index.json                  # Registry index
├── botdetection/
│   ├── atoms/
│   └── molecules/
└── packages/v3/index.json      # NuGet feed (BaGet proxy)
```

## Signal-Based Composition

Signals create a dependency mesh between manifests, enabling automatic composition.

### Signal Contract Graph

```yaml
# useragent-sensor.atom.yaml
emits:
  on_complete:
    - key: "detection.useragent.confidence"
    - key: "detection.useragent.bot_type"

# inconsistency-constrainer.atom.yaml
listens:
  required:
    - "detection.useragent.confidence"  # Depends on UserAgentSensor
```

This creates a directed graph:
```
UserAgentSensor ──emits──> detection.useragent.* ──listens──> InconsistencyConstrainer
```

### Automatic Composition

```csharp
public class ManifestComposer
{
    public CompositionGraph BuildGraph(IEnumerable<AtomManifest> manifests)
    {
        var graph = new CompositionGraph();

        foreach (var manifest in manifests)
        {
            graph.AddNode(manifest);

            // Find dependencies via signal contracts
            foreach (var required in manifest.Listens.Required)
            {
                var providers = manifests
                    .Where(m => m.EmitsSignal(required))
                    .ToList();

                foreach (var provider in providers)
                {
                    graph.AddEdge(provider, manifest, required);
                }
            }
        }

        return graph;
    }

    public IEnumerable<AtomManifest> TopologicalSort(CompositionGraph graph)
    {
        // Returns manifests in execution order
        // Atoms that emit signals run before those that listen
    }

    public ValidationResult Validate(CompositionGraph graph)
    {
        var errors = new List<string>();

        // Check for missing dependencies
        foreach (var node in graph.Nodes)
        {
            foreach (var required in node.Manifest.Listens.Required)
            {
                if (!graph.HasProvider(required))
                {
                    errors.Add($"{node.Name} requires '{required}' but no provider found");
                }
            }
        }

        // Check for cycles
        if (graph.HasCycles())
        {
            errors.Add("Circular dependency detected");
        }

        return new ValidationResult(errors);
    }
}
```

### Dynamic Discovery

Find what manifests are compatible at runtime:

```csharp
public interface IManifestDiscovery
{
    // What manifests can I add that work with my current set?
    IEnumerable<AtomManifest> FindCompatible(
        IEnumerable<AtomManifest> current,
        IEnumerable<AtomManifest> available);

    // What's missing to make this manifest work?
    IEnumerable<string> FindMissingSignals(
        AtomManifest manifest,
        IEnumerable<AtomManifest> available);

    // What breaks if I remove this manifest?
    IEnumerable<AtomManifest> FindDependents(
        AtomManifest manifest,
        IEnumerable<AtomManifest> current);
}
```

### Plugin Marketplace

```yaml
# Premium detector from private registry
# mostlylucid.net/premium/atoms/advanced-behavioral.atom.yaml
name: "AdvancedBehavioralAnalyzer"
version: "2.0.0"

implementation:
  nuget:
    package: "Mostlylucid.Premium.Detectors"
    version: "^2.0.0"
    source: "https://premium.mostlylucid.net/v3/index.json"  # Private feed
    requires_license: true

# Signals show it integrates with existing pipeline
listens:
  required:
    - "detection.behavioral.confidence"  # Builds on free behavioral
  optional:
    - "detection.useragent.*"

emits:
  on_complete:
    - key: "detection.advanced_behavioral.confidence"
      # Downstream atoms can listen to this
```

### Runtime Composition

```csharp
public class DynamicPipeline
{
    private readonly IManifestRegistry _registry;
    private readonly IManifestComposer _composer;

    public async Task<Pipeline> BuildPipelineAsync(string[] manifestSignatures)
    {
        // 1. Load requested manifests
        var manifests = await Task.WhenAll(
            manifestSignatures.Select(s => _registry.LoadAsync(s)));

        // 2. Build composition graph
        var graph = _composer.BuildGraph(manifests);

        // 3. Validate - are all dependencies satisfied?
        var validation = _composer.Validate(graph);
        if (!validation.IsValid)
        {
            // Suggest fixes
            foreach (var missing in validation.MissingSignals)
            {
                var providers = await _registry.FindProvidersAsync(missing);
                _logger.LogWarning(
                    "Missing signal '{Signal}'. Available providers: {Providers}",
                    missing, string.Join(", ", providers.Select(p => p.Name)));
            }
            throw new CompositionException(validation);
        }

        // 4. Order for execution
        var ordered = _composer.TopologicalSort(graph);

        // 5. Resolve implementations
        var atoms = await Task.WhenAll(
            ordered.Select(m => _resolver.ResolveImplementationAsync(m)));

        return new Pipeline(atoms);
    }
}
```

### Hot Reload

```csharp
public class ManifestWatcher : BackgroundService
{
    private readonly FileSystemWatcher _watcher;
    private readonly IPipelineManager _pipelines;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _watcher.Changed += async (s, e) =>
        {
            // Manifest file changed
            var manifest = await LoadManifestAsync(e.FullPath);

            // Recompose affected pipelines
            await _pipelines.RecomposeAsync(manifest);

            _logger.LogInformation(
                "Hot-reloaded manifest {Name}@{Version}",
                manifest.Name, manifest.Version);
        };

        _watcher.EnableRaisingEvents = true;
        await Task.Delay(Timeout.Infinite, ct);
    }
}
```

## Benefits

1. **Separation of Concerns**: Contracts separate from implementations
2. **Human-Readable**: YAML manifests are easy to review/diff
3. **Independent Evolution**: Update manifests without rebuilding packages
4. **Flexible Distribution**: Git, HTTP, or NuGet for manifests
5. **Standard Packages**: Implementations use standard NuGet
6. **Version Control**: SemVer for both manifests and packages
7. **Reproducible**: Lock files pin exact versions
8. **Self-Hostable**: Simple static files for manifests, BaGet for packages
9. **Signal Mesh**: Automatic dependency discovery via signal contracts
10. **Dynamic Composition**: Runtime pipeline assembly from available manifests
11. **Hot Reload**: Update manifests without restart
12. **Plugin Ecosystem**: Premium packages on private registries
