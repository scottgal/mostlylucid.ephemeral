# Ephemeral Taxonomy

This document captures the shared vocabulary for Ephemeral atoms, signals, molecules, and escalation. It is a reference
for how to describe the system, not a strict API checklist.

## Core nouns

### Substrate

The authoritative store of what the system knows (or can reconstruct), with versioning and provenance.

Substrate holds:

- Evidence: raw artifacts plus pointers (file, URL, frame ranges, offsets, hashes)
- Signals: deterministic facts and metrics (typed key-value, confidence, provenance)
- Proposals: probabilistic outputs (typed, confidence, evidence refs)
- Decisions: acceptance or rejection results plus rationale and policy version
- Run records: what ran, costs, timings, model versions, errors
- Feedback events: clicks, dwell, thumbs, escalations, overrides

### Lens

A policy bundle that defines how to interpret and operate over the substrate for a given use-case.

A lens specifies:

- Query kinds and output modes
- Retrieval strategy (BM25, vector, hybrid, graph)
- Scoring weights and fusion (RRF, weighted sum, rules)
- Budgets (token, time, cost) and confidence thresholds
- LLM allowed or required or never rules
- Which signals are relevant (projection)
- Rendering style (docs list vs answer vs JSON)

### SignalSink

A persistence target for durable writes. EscalatorAtoms can promote outputs across multiple sinks.

Examples:

- Relational DB (signals, decisions, metadata)
- Vector DB (embeddings, multi-vectors)
- Object store (evidence blobs)
- Audit log (append-only decisions)
- Queue or ticket system (human review)
- Metrics or telemetry backend

## Execution model

### Atom

Smallest executable unit. Atoms are typed by determinism and write authority.

Every atom declares:

- Reads: what it can read (evidence, signals, proposals, decisions, feedback)
- Writes: what it can emit
- Determinism: deterministic or probabilistic
- Persistence: ephemeral-only, persistable-via-escalation, or direct-write-allowed
- Budget: time, token, cost ceilings
- Evidence requirements: what it must attach to outputs

#### Atom kinds

1. SensorAtom (deterministic)
   Extracts signals and or evidence pointers from sources.

2. ExtractorAtom (deterministic, structural)
   Turns raw content into semantic units (stable segmentation).

3. EmbedderAtom (deterministic-ish)
   Produces embeddings for units under a named embedding space.

4. RetrieverAtom (deterministic)
   Retrieves candidate units or docs under a lens.

5. ProposerAtom (probabilistic)
   Produces proposals (never truth).

6. ConstrainerAtom (deterministic)
   Validates and selects among proposals; may compute derived deterministic signals.

7. RankerAtom (deterministic)
   Re-scores and re-orders candidates using signals and learned weights.

8. RendererAtom (deterministic)
   Turns selected evidence and decisions into an output artifact.

9. CoordinatorAtom (deterministic)
   Plans and orchestrates which atoms run, in what order, under what budgets.

10. FeedbackAtom (deterministic)
    Consumes feedback events and updates scoring models or weights.

11. EscalatorAtom (deterministic, privileged)
    Promotes ephemeral outputs into durable stores across multiple SignalSinks.
    This is the membrane between ephemeral compute and durable truth.

12. GuardAtom (deterministic)
    Hard safety and compliance gates.

Key rule:

- Probabilistic atoms can only emit proposals.
- Only deterministic atoms may decide or persist, and durable persistence is typically via EscalatorAtoms.

Implementation note: Atom kinds are extensible in code; register new kinds with `AtomKind.Register("custom.kind")`.

### Molecule

A bounded composition (graph) of atoms with a single contract: what it produces and what gets persisted.

Molecule types:

- IngestionMolecule: evidence to units to signals to embeddings to persisted substrate updates
- QueryMolecule: user query to retrieval to ranking to evidence pack
- SynthesisMolecule: evidence pack to proposals to constrained selection to rendered answer
- FeedbackMolecule: outcomes to updated weights and calibration
- EscalationMolecule: low confidence or contradiction to human review sink plus audit trail
- MaintenanceMolecule: re-embed, re-score, re-index, recompute signals

## Persistence tiers

### Ephemeral outputs

Short-lived, run-scoped artifacts:

- Candidate sets
- Intermediate scores
- Raw proposals
- Temporary summaries
- Partial signals from coordinators

### Durable truth

Persisted, versioned artifacts:

- Evidence refs (immutable)
- Signals (typed plus provenance)
- Decisions (append-only)
- Embeddings (replaceable, multi-space)
- Feedback models (versioned)

Promotion boundary: EscalatorAtoms (and sometimes GuardAtoms) decide what crosses.

## Data types (signal contract layer)

### EvidenceRef

- Source id, hash, offsets or ranges (text offsets, frame ranges, timestamps)
- Capture metadata (tool version, extraction settings)

### Signal

- Key, typed value, confidence, provenance (EvidenceRefs), computed-by, version

### Proposal

- Key or type, payload, confidence, evidence refs, model id or version, trace

### Decision

- Accepted or rejected or merged, rationale, policy version, references to proposals or signals or evidence

### CandidateSet and EvidencePack

- Bounded list of items with scores and why this matched
- The only thing an LLM should see in synthesis mode

## Pack mapping

A pack is:

- Lens defaults, molecules, renderers, UI integration
- Plus specific sensors or extractors for that modality

Examples:

- Docs Pack: Markdown or PDF extractors plus doc viewer renderer
- Image Pack: image sensors plus OCR and filmstrip units plus evidence viewer
- Video Pack: shot or scene segmentation plus keyframe units plus timestamped evidence
- Slack Pack: thread evidence units plus permissions guard plus concise renderer
- Voice Pack: ASR sensor plus turn-taking lens plus escalation plus TTS renderer
- BotDetection Pack: request sensors plus behavior signals plus risk lens plus action renderer

## Guiding principle

Proposers propose. Constrainers decide. Escalators persist. Lenses choose. The substrate remembers.

Implementation note: `mostlylucid.ephemeral.atoms.taxonomy` provides the shared contracts, shard descriptors, and base
types (including `MultiTaxonomyAtom`), while each atom kind ships as its own package under
`mostlylucid.ephemeral.atoms.taxonomy.*`.
