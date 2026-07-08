# Breaking-change report: Mostlylucid.Ephemeral 2.6.4 → 2.9.0

**Verdict: NON-BREAKING for a 2.6.4 consumer — 0 breaking changes.**

`v2.6.4` is a direct linear ancestor of the 2.9.0 release commit, so `git diff v2.6.4 HEAD`
is a complete and authoritative diff. Every finding below was verified against `git` (an
empty diff for a type's defining file means byte-identical public surface).

The only source files that changed under `src/` between 2.6.4 and 2.9.0 are:

- **Added** — the entire `Mostlylucid.Ephemeral.Atoms.Llm` package (new in 2.7.0; split
  into `.Llm.Abstractions` + `.Llm.Coordinator` + `.Llm` meta in 2.9.0).
- **Modified (additive only)** — `Atoms/DetectorAtom.cs` in `Atoms.Taxonomy`
  (`Raise(...)` helper overloads on `DetectorAtomBase` + extra runtime signal emissions).
- **Modified (packaging only)** — `sqlite.singlewriter` csproj.

## Per-type findings

| Type / member | Result |
|---|---|
| `SignalSink` — ctor `(int maxCapacity = 1000, TimeSpan? maxAge = null)`, `Raise`, `Sense`, `Detect` | Byte-identical. No break. (`ReadHint` does not exist in either version.) |
| `TypedSignalSink<TPayload>`, `SignalKey<TPayload>`, `SignalPropagation` | Byte-identical. No break. (`SignalEvent` is a non-generic record struct in both versions.) |
| `EphemeralKeyedWorkCoordinator<T, TKey>` — ctor `(keySelector, processor, EphemeralOptions)`, `EnqueueAsync`, `GetSignalsByPattern`, `GetSignalsByKey`, `OperationFinalized`, snapshots | Byte-identical. Generic order still `(T, TKey)` — no swap. No break. |
| `EphemeralOperation` — `Signal`, `EmitCaused`, `HasSignal` | Byte-identical. No break. |
| `EphemeralOptions` — `MaxTrackedOperations` (=200), `MaxOperationLifetime` (=5 min), `Signals`, others | Byte-identical, defaults unchanged. No break. |
| Taxonomy — `DetectionLedger`, `DetectionContribution`, `IDetectorAtom` | `DetectionLedger` byte-identical; `IDetectorAtom` / `DetectionContribution` unchanged. No break. |
| Taxonomy — `DetectorAtomBase` | **Additive**: two new `protected Raise(...)` overloads (auto-prepend `Name`). See note below. |

## Non-breaking additions (informational)

- New `Mostlylucid.Ephemeral.Atoms.Llm*` packages.
- New `protected Raise(...)` helper overloads on `DetectorAtomBase`.
- New runtime signal strings emitted by `DetectorOrchestrator` (`risk.current_score:*`,
  `contribution.<detector>.*`).

## Only nuance for consumers

A consumer that has defined its **own** `Raise(...)` method on a subclass of
`DetectorAtomBase` may now see compiler warning **CS0108** (member hides inherited member).
This is a **warning, not an error**, and requires no migration — add `new` to silence it if
desired.

## Migration required

**None.** A 2.6.4 consumer can bump the entire family to 2.9.0 with no source changes.
