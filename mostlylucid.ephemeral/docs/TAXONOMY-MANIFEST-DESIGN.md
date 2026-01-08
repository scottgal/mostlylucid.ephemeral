# Unified Taxonomy Manifest System

## Version 2.0 - Breaking Changes

This is a **major version** that introduces breaking changes to enable cross-project portability.

### Breaking Changes Summary

| Change | v1.x | v2.0 |
|--------|------|------|
| **SignalSink** | Standalone entity with own lifetime | **REMOVED** - replaced by SignalView |
| **Signal Ownership** | Unclear | Atoms own signals, Ledger holds them |
| **Signal Storage** | In-memory only | EntityLedger persisted to RDBMS + Vector |
| **Cross-Coordinator Signals** | Via shared SignalSink | Via shared SignalView over Ledgers |
| **Signal Lifetime** | Managed by SignalSink window | Managed by Coordinator/Ledger |

### Migration Guide

```csharp
// v1.x - SignalSink with separate lifetime
var sink = new SignalSink();
var coordinator = new EphemeralWorkCoordinator<T>(handler, new EphemeralOptions { Signals = sink });
sink.Raise("signal.key");  // Signal in sink

// v2.0 - EntityLedger with SignalView
var ledger = ledgerFactory.Create("image");
var view = new SignalView(ledger, SignalViewOptions.All);
ledger.Record("signal.key", value, salience: 0.8, sourceAtom: "MySensor");
var signals = view.GetSignals("signal.*");  // Query via view
```

### New Core Concepts

1. **ISignalSource** - Live signal source (atoms in coordinators)
2. **SignalView** - Lightweight query over live signals (no separate lifetime)
3. **LedgerAtom** - Bridges live signals to persistent storage
4. **EntityLedger** - Persisted entity (RDBMS + Vector linked)
5. **VectorLens** - Multiple embeddings per entity (text, image, clip, signal, etc.)
6. **AtomManifest** - YAML contract for atoms
7. **MoleculeManifest** - YAML contract for atom assemblages

### Complete Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           LIVE PROCESSING                                    │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                    COORDINATORS (manage atom lifetimes)              │   │
│  │                                                                      │   │
│  │   ┌───────────┐   ┌───────────┐   ┌───────────┐   ┌───────────┐    │   │
│  │   │  Sensor   │   │ Extractor │   │ Proposer  │   │ Constrainer│   │   │
│  │   │  Atom     │   │   Atom    │   │   Atom    │   │    Atom    │   │   │
│  │   │           │   │           │   │           │   │            │   │   │
│  │   │ signals:  │   │ signals:  │   │ signals:  │   │ signals:   │   │   │
│  │   │ sal=0.3   │   │ sal=0.7   │   │ sal=0.9   │   │ sal=0.6    │   │   │
│  │   │ (ephemeral)│   │ (maybe)   │   │ (escalate)│   │ (ephemeral)│   │   │
│  │   └───────────┘   └───────────┘   └───────────┘   └───────────┘    │   │
│  │         │               │               │               │           │   │
│  └─────────┼───────────────┼───────────────┼───────────────┼───────────┘   │
│            │               │               │               │                │
│            └───────────────┴───────────────┴───────────────┘                │
│                                    │                                         │
│                                    ▼                                         │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                    SIGNAL VIEW (query over live signals)             │   │
│  │                                                                      │   │
│  │   Pattern: "*"  |  Salience: > 0.0  |  Sources: all                 │   │
│  │   → Returns all live signals from all atoms                          │   │
│  │   → Signals don't care - VIEW decides what to show                  │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                    │                                         │
│                                    ▼                                         │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                    LEDGER ATOM (escalation bridge)                   │   │
│  │                                                                      │   │
│  │   Salience Threshold: 0.8                                           │   │
│  │   → Sensor (0.3): EPHEMERAL - dies with atom                        │   │
│  │   → Extractor (0.7): EPHEMERAL - dies with atom                     │   │
│  │   → Proposer (0.9): ESCALATE - persist to ledger ✓                  │   │
│  │   → Constrainer (0.6): EPHEMERAL - dies with atom                   │   │
│  │                                                                      │   │
│  │   The SYSTEM decides, not the signals.                              │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                    │                                         │
└────────────────────────────────────┼────────────────────────────────────────┘
                                     │
                                     ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                           PERSISTENT STORAGE                                 │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                    ENTITY LEDGER                                     │   │
│  │                                                                      │   │
│  │   EntityId: img_abc123                                              │   │
│  │   EntityType: image                                                  │   │
│  │                                                                      │   │
│  │   Signals:                                                           │   │
│  │   ┌─────────────────────────────────────────────────────────────┐   │   │
│  │   │ vision.caption = "A sunset over mountains"  | sal=0.9      │   │   │
│  │   │ (only high-salience signals make it here)                   │   │   │
│  │   └─────────────────────────────────────────────────────────────┘   │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                    │                                         │
│            ┌───────────────────────┴───────────────────────┐                │
│            ▼                                               ▼                │
│  ┌─────────────────────┐                     ┌─────────────────────┐       │
│  │ RDBMS (SQLite/PG)   │                     │ Vector Store        │       │
│  │                     │                     │                     │       │
│  │ - Entity metadata   │◄────── LINKED ─────►│ - Text embedding    │       │
│  │ - Signal values     │   (by EntityId)     │ - Image embedding   │       │
│  │ - Timestamps        │                     │ - CLIP embedding    │       │
│  │ - Salience scores   │                     │ - Signal embedding  │       │
│  └─────────────────────┘                     └─────────────────────┘       │
│                                                                              │
│  Multiple VECTOR LENSES per entity (different perspectives)                 │
└─────────────────────────────────────────────────────────────────────────────┘
```

**Key Insight: Signals Don't Care**

The signals are neutral - they don't know or care about their fate. The SYSTEM decides:
- **High salience** → Escalate to EntityLedger (persisted)
- **Low salience** → Ephemeral (dies with source atom)

This separation means:
- Atoms focus on producing accurate signals with correct salience
- Coordinators focus on orchestrating atoms
- Views focus on querying what's needed
- LedgerAtom focuses on persistence policy
- EntityLedger focuses on storage and retrieval

---

## Overview

This document describes the unified YAML-based contract system for atoms, molecules, and coordinators in the Mostlylucid.Ephemeral library.

---

## Architecture Thinking (Design Rationale)

This section captures the key architectural decisions and reasoning to maintain alignment as we co-develop.

### Problem Statement

We have multiple projects (BotDetection, DocSummarizer, ImageSummarizer, DataSummarizer) that all do similar things:
1. Process **entities** (requests, documents, images, data rows)
2. Run **detectors/analyzers** (atoms) against entities
3. Accumulate **signals** from processing
4. Make **decisions** based on accumulated signals
5. **Learn** from outcomes to improve future processing

Currently, each project has its own patterns, making:
- Code reuse difficult (a detector from BotDetection can't easily work in DocSummarizer)
- Signal handling inconsistent (different lifetime management)
- Storage patterns divergent (each project has its own escalation)

### Core Insight #1: SignalSink Is A View

**Key Principle**: A SignalSink is a **VIEW** over atom signals, not a container.

**Mental Model**:
- **Atoms** are the SOURCE of signals (they emit and own signals)
- **SignalSink** is a QUERY INTERFACE / VIEW over those signals
- Multiple sinks can provide different views over the same underlying signals
- Sinks can span multiple coordinators (shared views)

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        SIGNAL ARCHITECTURE                               │
│                                                                          │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                    ATOM SIGNAL SOURCES                           │   │
│  │                                                                  │   │
│  │  ┌──────────┐   ┌──────────┐   ┌──────────┐   ┌──────────┐     │   │
│  │  │ Atom A   │   │ Atom B   │   │ Atom C   │   │ Atom D   │     │   │
│  │  │ signals: │   │ signals: │   │ signals: │   │ signals: │     │   │
│  │  │  a.1     │   │  b.1     │   │  c.1     │   │  d.1     │     │   │
│  │  │  a.2     │   │  b.2     │   │  c.2     │   │  d.2     │     │   │
│  │  └──────────┘   └──────────┘   └──────────┘   └──────────┘     │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                              │                                          │
│                              ▼                                          │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                    SIGNAL SINKS (VIEWS)                          │   │
│  │                                                                  │   │
│  │  ┌─────────────────────────┐   ┌─────────────────────────────┐ │   │
│  │  │ Sink: FastPath          │   │ Sink: Learning              │ │   │
│  │  │ View: a.*, b.1          │   │ View: *.* WHERE salience>0.8│ │   │
│  │  │ Used by: Coord A        │   │ Used by: Coord A, Coord B   │ │   │
│  │  └─────────────────────────┘   └─────────────────────────────┘ │   │
│  │                                                                  │   │
│  │  ┌─────────────────────────┐   ┌─────────────────────────────┐ │   │
│  │  │ Sink: Metrics           │   │ Sink: FullAnalysis          │ │   │
│  │  │ View: all signals       │   │ View: c.*, d.*              │ │   │
│  │  │ Used by: Coord C        │   │ Used by: Coord B            │ │   │
│  │  └─────────────────────────┘   └─────────────────────────────┘ │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                                                                          │
│  COORDINATORS reference sinks (views) they need:                        │
│  - Coord A uses: FastPath, Learning                                     │
│  - Coord B uses: Learning, FullAnalysis                                 │
│  - Coord C uses: Metrics                                                 │
│                                                                          │
│  Sink dies when NO coordinator references it                            │
└─────────────────────────────────────────────────────────────────────────┘
```

**View Properties**:
- **Filter** - Which signals are visible (pattern, salience threshold)
- **Scope** - Which atoms/coordinators contribute signals
- **Window** - Time-based or count-based signal retention
- **Sharing** - Multiple coordinators can share a view

This enables:
- **Escalation** - Learning sink sees high-salience signals from production
- **Cross-cutting** - Metrics sink sees all signals for observability
- **Isolation** - Fast-path sink only sees stage-0 signals (fast access)
- **Composition** - Sink views can overlap, providing different perspectives

### Core Insight #2: Atoms Are Context-Agnostic

An atom doesn't need to know WHAT it's processing - only that the required signals exist.

```
UserAgentSensor doesn't care if it's in:
- BotDetection (processing HTTP requests)
- DocSummarizer (processing document uploads)
- DataSummarizer (processing API calls)

It only needs: "http.headers.useragent" signal exists
It emits: "detection.useragent.*" signals

The atom is portable because it speaks SIGNALS, not ENTITIES.
```

### Core Insight #3: Entity Ledger Is The Bridge

Each entity gets a **ledger** that accumulates signals. The ledger is:
- Scoped to entity lifetime
- Queryable by atoms (to check dependencies)
- The source of truth for "what signals exist"

This decouples atoms from each other - they communicate via the ledger, not directly.

### Core Insight #4: Escalation Is Processing, Not Just Storage

**Previous Assumption**: Escalation = persist to database.

**Correct Model**: Escalation is promotion through a **salience pipeline**. Each level:
1. Applies salience threshold (drops low-value signals)
2. May apply priority boost
3. Routes to next processor (another atom, molecule, coordinator, OR storage)

Storage is the FINAL escalation target, not the only one.

### Core Insight #5: YAML Manifests Are Universal Contracts

Every atom/molecule/coordinator has a YAML manifest declaring:
- What signals it reads (dependencies)
- What signals it emits (outputs)
- How signals are preserved (echo, escalate, propagate)
- Execution constraints (budget, lane, priority)

This enables:
- Static analysis of signal flow
- LLM-driven orchestration
- Cross-project portability validation

---

## Cross-Project Architecture Goal

**Critical**: The architecture must enable portability across all Mostlylucid projects:
- **DocSummarizer** - Document processing
- **ImageSummarizer** (DocSummarizer.Images) - Image analysis
- **DataSummarizer** - Structured data processing
- **BotDetection** - Request analysis

A detector/atom from ANY project should work in ANY other project. This requires:
1. **Common manifest schema** - Same YAML format everywhere
2. **Common signal ledgers** - Entities accumulate signals the same way
3. **Common RAG storage** - Escalated signals flow to unified storage
4. **Portable atoms** - Atoms are context-agnostic, work anywhere

---

## Critical Architectural Principle: Coordinator Owns Everything

**SignalSinks have NO separate lifetime from Coordinators.**

```
WRONG (Anti-pattern):
┌─────────────────┐     ┌─────────────────┐
│ SignalSink      │     │ Coordinator     │
│ - own lifetime  │◄────│ - references    │
│ - signal count  │     │   sink          │
│ - signal window │     └─────────────────┘
└─────────────────┘
   ↑ BAD: Sink outlives coordinator, signals leak

RIGHT (Correct):
┌──────────────────────────────────────────────────────────────┐
│ COORDINATOR (single owner of all state)                       │
│                                                               │
│   ┌───────────────────────────────────────────────────────┐  │
│   │ SignalSink (INTERNAL - no separate lifetime)          │  │
│   │ - Coordinator controls window, eviction, lifetime     │  │
│   │ - NO independent signal count                         │  │
│   │ - Dies when Coordinator dies                          │  │
│   └───────────────────────────────────────────────────────┘  │
│                                                               │
│   ┌───────────┐  ┌───────────┐  ┌───────────┐               │
│   │  Atom A   │  │  Atom B   │  │  Atom C   │               │
│   │  signals  │  │  signals  │  │  signals  │               │
│   └─────┬─────┘  └─────┬─────┘  └─────┬─────┘               │
│         │              │              │                      │
│         └──────────────┼──────────────┘                      │
│                        ▼                                     │
│              Coordinator's Internal Sink                     │
│                        │                                     │
│         ┌──────────────┼──────────────┐                      │
│         ▼              ▼              ▼                      │
│    [Ephemeral]   [Escalate]    [Propagate]                  │
│    (dies here)   (to next)     (to parent)                  │
└──────────────────────────────────────────────────────────────┘
```

### Key Rules

1. **No standalone SignalSinks** - Sink is ALWAYS internal to a Coordinator
2. **Coordinator manages ALL signal lifecycle** - window size, eviction, TTL
3. **Atoms emit to their Coordinator** - not to any external sink
4. **When Coordinator dies** - ALL its signals die (unless escalated)
5. **Escalation is the ONLY escape** - Signals survive ONLY via escalation to another Coordinator or storage

---

## Entity Ledgers: The Universal Signal Accumulator

Each project processes **entities** (images, documents, requests, data rows). Entities accumulate signals from atoms as they flow through the pipeline. This is the **Entity Ledger**.

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        ENTITY LEDGER                                     │
│                                                                          │
│  Entity ID: img_abc123 (or doc_xyz789, req_456, etc.)                   │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │ Accumulated Signals (from all atoms that processed this entity)    │ │
│  │                                                                     │ │
│  │  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐    │ │
│  │  │ identity.format │  │ ocr.text        │  │ vision.caption  │    │ │
│  │  │ salience: 0.95  │  │ salience: 0.72  │  │ salience: 0.88  │    │ │
│  │  │ source: sensor  │  │ source: extract │  │ source: proposer│    │ │
│  │  └─────────────────┘  └─────────────────┘  └─────────────────┘    │ │
│  │                                                                     │ │
│  │  ┌─────────────────┐  ┌─────────────────┐                         │ │
│  │  │ color.palette   │  │ detection.score │                         │ │
│  │  │ salience: 0.45  │  │ salience: 0.91  │                         │ │
│  │  │ source: sensor  │  │ source: ranker  │                         │ │
│  │  └─────────────────┘  └─────────────────┘                         │ │
│  └────────────────────────────────────────────────────────────────────┘ │
│                                                                          │
│  Ledger Operations:                                                      │
│  - AccumulateSignal(signal, salience, source_atom)                      │
│  - GetSignals(pattern) → filtered signals                               │
│  - GetHighSalienceSignals(threshold) → only high-value signals          │
│  - EscalateToRAG() → persist to vector store                            │
└─────────────────────────────────────────────────────────────────────────┘
```

### Entity Ledger Rules

1. **One ledger per entity** - Each image/document/request/row gets its own ledger
2. **Ledger lifetime = entity lifetime** - When processing completes, ledger can be:
   - **Discarded** (ephemeral processing)
   - **Escalated** (high-salience signals to RAG)
   - **Cached** (for deduplication/reuse)
3. **Atoms write to ledger** - via Coordinator routing
4. **Ledger is queryable** - Atoms can read signals from prior atoms

### Cross-Project Portability via Ledgers

```
┌─────────────────────────────────────────────────────────────────────────┐
│                   PORTABLE ATOM PATTERN                                  │
│                                                                          │
│  ┌──────────────────────────────────────────────────────────────────┐   │
│  │ UserAgentSensor (from BotDetection)                               │   │
│  │                                                                   │   │
│  │ READS:  http.headers.useragent                                   │   │
│  │ EMITS:  detection.useragent.confidence                           │   │
│  │         detection.useragent.is_bot                               │   │
│  │         detection.useragent.bot_type                             │   │
│  └──────────────────────────────────────────────────────────────────┘   │
│                                                                          │
│  Can be used in:                                                         │
│                                                                          │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐         │
│  │ BotDetection    │  │ DocSummarizer   │  │ DataSummarizer  │         │
│  │                 │  │                 │  │                 │         │
│  │ Entity: Request │  │ Entity: Doc     │  │ Entity: Row     │         │
│  │                 │  │                 │  │                 │         │
│  │ IF request has  │  │ IF doc upload   │  │ IF API request  │         │
│  │ UA header       │  │ has UA header   │  │ has UA header   │         │
│  │ → atom runs     │  │ → atom runs     │  │ → atom runs     │         │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘         │
│                                                                          │
│  The atom doesn't care WHAT the entity is - only that the required      │
│  signals exist in the ledger (or context).                              │
└─────────────────────────────────────────────────────────────────────────┘
```

### RAG Storage: The Final Escalation Target

All projects share a common RAG storage pattern for persisted signals:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                      UNIFIED RAG STORAGE                                 │
│                                                                          │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │ Vector Store (embeddings)                                          │ │
│  │  - Entity embeddings (CLIP, text, etc.)                           │ │
│  │  - Signal embeddings (for similarity search)                       │ │
│  └────────────────────────────────────────────────────────────────────┘ │
│                                                                          │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │ Signal Store (SQLite/Postgres)                                     │ │
│  │  - Entity ID, Signal Key, Value, Salience, Source Atom, Timestamp │ │
│  │  - Indexed by entity, signal pattern, salience                    │ │
│  └────────────────────────────────────────────────────────────────────┘ │
│                                                                          │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │ Learning Store (patterns, weights)                                 │ │
│  │  - Learned patterns from high-salience signals                    │ │
│  │  - Feature weights for heuristic models                           │ │
│  │  - Feedback loop data                                              │ │
│  └────────────────────────────────────────────────────────────────────┘ │
│                                                                          │
│  All projects escalate to the SAME storage schema.                      │
│  Atoms from any project can read from shared RAG.                       │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Core Principle: Signal Ownership

**Atoms own their signals.** This is the fundamental architectural principle:

```
┌─────────────────────────────────────────────────────────────────┐
│ COORDINATOR (manages atom lifetimes)                            │
│                                                                 │
│   ┌─────────────────────┐     ┌─────────────────────┐          │
│   │ ATOM A              │     │ ATOM B              │          │
│   │                     │     │                     │          │
│   │  ┌───────────────┐  │     │  ┌───────────────┐  │          │
│   │  │ Signal A.1    │──┼────►│  │ (listens)     │  │          │
│   │  │ Signal A.2    │  │     │  │ Signal B.1    │──┼──► ECHO  │
│   │  │ Signal A.3    │  │     │  │ Signal B.2    │  │          │
│   │  └───────────────┘  │     │  └───────────────┘  │          │
│   └─────────────────────┘     └─────────────────────┘          │
│                                                                 │
│   When Atom A dies → Signals A.1, A.2, A.3 die (unless echoed) │
│   When Atom B dies → Signals B.1, B.2 die (unless escalated)   │
└─────────────────────────────────────────────────────────────────┘
```

### Signal Lifecycle Rules

1. **Atom creates signals** - Atoms emit signals during execution
2. **Atom owns signals** - Signals belong to their emitting atom
3. **Atom death = Signal death** - When an atom terminates, its signals die
4. **Echo preserves signals** - Signals echoed to another atom survive
5. **Escalation persists signals** - Escalated signals survive to durable storage
6. **Coordinator observes** - Coordinator can observe signals but doesn't own them

### Signal Preservation Patterns

| Pattern | Description | Use Case |
|---------|-------------|----------|
| **Ephemeral** | Signal dies with atom | Intermediate computation |
| **Echo** | Signal copied to another atom | Cross-atom communication |
| **Escalate** | Signal promoted to higher-level processing | Salience filtering, learning |
| **Propagate** | Signal forwarded to molecule | Aggregate output |

### Escalation as Salience Pipeline

Escalation is NOT just storage - it's a **salience-increasing processing pipeline**:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                     ESCALATION PIPELINE                                  │
│                                                                          │
│  Level 0 (Atoms)          Level 1 (Molecules)      Level 2 (Coordinator) │
│  ┌──────────────┐         ┌──────────────┐        ┌──────────────┐      │
│  │ Signal A     │         │              │        │              │      │
│  │ salience=0.3 │──X      │              │        │              │      │
│  │ (dropped)    │         │              │        │              │      │
│  └──────────────┘         │              │        │              │      │
│                           │              │        │              │      │
│  ┌──────────────┐         │  Signal B'   │        │              │      │
│  │ Signal B     │────────►│  salience=0.7│───X    │              │      │
│  │ salience=0.6 │ boost   │  (dropped)   │        │              │      │
│  └──────────────┘ +0.1    └──────────────┘        │              │      │
│                                                    │              │      │
│  ┌──────────────┐         ┌──────────────┐        │  Signal C''  │      │
│  │ Signal C     │────────►│  Signal C'   │───────►│  salience=0.95│     │
│  │ salience=0.8 │ boost   │  salience=0.9│ boost  │  (persisted) │      │
│  └──────────────┘ +0.1    └──────────────┘ +0.05  └──────────────┘      │
│                                                                          │
│  Each escalation level:                                                  │
│  - Applies salience threshold (drops low-salience signals)              │
│  - May apply priority boost                                              │
│  - May batch for efficiency                                              │
│  - May route to different targets (learning, analysis, storage)         │
└─────────────────────────────────────────────────────────────────────────┘
```

**Escalation Targets:**
- **Another atom/molecule** - For further analysis (e.g., `llm_analyzer`)
- **Learning coordinator** - For model training (e.g., `salience_learner`)
- **Storage layer** - For persistence (e.g., `signal_database`)

**Example: Image Analysis Escalation**
```
ColorSensor (atom)
  → emits: color.dominant (salience=0.4)
  → escalate to: VisionMolecule (threshold=0.5) → DROPPED

OcrExtractor (atom)
  → emits: ocr.text (salience=0.7)
  → escalate to: VisionMolecule (threshold=0.5) → PROMOTED
  → VisionMolecule escalates to: LearningCoordinator (threshold=0.8) → DROPPED

VisionLlmProposer (atom)
  → emits: vision.caption (salience=0.9)
  → escalate to: LearningCoordinator (threshold=0.8) → PROMOTED
  → LearningCoordinator escalates to: SignalDatabase → PERSISTED
```

## Entity Hierarchy

```
Coordinator (manages atom lifetimes, observes signals)
    │
    └── Molecule (assemblage of atoms with aggregate contract)
            │
            └── Atom (fundamental unit that OWNS its signals)
```

### Atom
- **Definition**: Fundamental unit of work (detector, analyzer, transformer)
- **Examples**: `UserAgentDetector`, `ColorWave`, `OcrExtractor`
- **Contract**: Single responsibility, **owns its emitted signals**
- **Lifecycle**: Created by coordinator, signals die when atom dies

### Molecule
- **Definition**: Assemblage of atoms forming a processing unit
- **Examples**: `IdentityWave` (combines multiple identity atoms), `DetectorPipeline`
- **Contract**: Aggregate of constituent atom contracts, **propagates selected signals**
- **Lifecycle**: Manages constituent atom lifetimes

### Coordinator
- **Definition**: Orchestrates molecules/atoms with scope and profile
- **Examples**: `ProfiledWaveCoordinator`, `EphemeralDetectionOrchestrator`
- **Contract**: Execution profile, lane configuration, timeout policies
- **Lifecycle**: Long-lived, manages all atom/molecule lifetimes

---

## Atom Manifest Schema (*.atom.yaml)

```yaml
# ==========================================
# ATOM MANIFEST - Universal Signal Contract
# ==========================================

# Identity
name: "UserAgentSensor"              # Unique identifier (matches class name)
version: "1.0.0"                     # Semantic version
description: "Extracts bot signals from User-Agent header"

# Taxonomy Classification
taxonomy:
  kind: sensor                       # sensor|extractor|embedder|retriever|proposer|constrainer|ranker|renderer|coordinator|feedback|escalator|guard
  determinism: deterministic         # deterministic|probabilistic
  persistence: ephemeral             # ephemeral|escalatable|direct_write

# Signal Scope (3-level hierarchy)
scope:
  sink: "botdetection"               # Top-level boundary (project/domain)
  coordinator: "detection"           # Processing unit context
  atom: "useragent"                  # This atom's unique name within coordinator

# Triggers (when to run)
triggers:
  requires:                          # ALL must be satisfied
    - signal: "http.request.received"
    - signal: "http.headers.useragent"
      condition: "HasValue"          # HasValue|IsNullOrWhiteSpace|>|<|>=|<=|==|!=
  signals:                           # Run when ANY of these exist
    - "detection.pipeline.started"
  skip_when:                         # Skip if ANY of these exist
    - signal: "detection.early_exit"
    - signal: "cache.hit.{scope.atom}"

# Signal Emissions (what this atom produces and OWNS)
# NOTE: All signals die when this atom dies, unless explicitly preserved
emits:
  on_start:                          # When atom begins execution
    - "atom.{scope.atom}.started"

  on_complete:                       # When atom succeeds
    - key: "detection.useragent.confidence"
      type: double
      description: "Bot detection confidence from UA analysis"
      confidence_range: [0.0, 1.0]
    - key: "detection.useragent.bot_type"
      type: string?
      description: "Detected bot type (if any)"
    - key: "detection.useragent.reasons"
      type: "List<string>"
      description: "Detection reason codes"

  on_failure:                        # When atom fails
    - "atom.{scope.atom}.failed"
    - key: "atom.{scope.atom}.error"
      type: string
      description: "Error message"

  conditional:                       # Context-dependent signals
    - key: "detection.useragent.verified_bot"
      type: bool
      when: "confidence > 0.9 && bot_type != null"
      description: "High-confidence bot identification"

# Signal Preservation (how signals survive atom death)
preserve:
  # Echo: Copy signals to another atom (signal survives in recipient)
  echo:
    - signal: "detection.useragent.confidence"
      to: "aggregator"               # Target atom that will own the copy
      when: "always"                 # always|on_complete|conditional

  # Escalate: Promote signals through salience pipeline
  # NOT just storage - escalation routes to further processing
  escalate:
    # Route high-confidence detections to learning coordinator
    - signal: "detection.useragent.verified_bot"
      to: "learning_coordinator"     # Target: atom, molecule, coordinator, or storage
      salience_threshold: 0.8        # Only escalate if salience >= 0.8
      priority_boost: 10             # Increase priority for learning queue
      when: "value == true"
      batch: true                    # Batch for efficiency
      batch_size: 50
      batch_timeout: "00:01:00"

    # Route bot signatures to analysis molecule for pattern extraction
    - signal: "detection.useragent.bot_type"
      to: "pattern_analysis_molecule"
      salience_threshold: 0.7
      when: "confidence > 0.7"

    # Final escalation to storage (only high-salience signals reach here)
    - signal: "detection.useragent.learned_pattern"
      to: "signal_database"          # Persistent storage
      salience_threshold: 0.9        # Very high threshold for persistence

  # Propagate: Forward to molecule's aggregate output
  propagate:
    - signal: "detection.useragent.confidence"
      as: "fast_path.useragent.confidence"  # Rename in molecule context

# Dependencies (signals this atom listens for from other atoms)
listens:
  required:                          # Must exist before atom can run
    - "http.headers.useragent"
  optional:                          # May use if available
    - "http.headers.accept_language"
    - "cache.patterns.bot_signatures"

# Escalation Rules
escalation:
  to_llm:                            # Escalation target name
    when:
      - signal: "detection.useragent.confidence"
        condition: "< 0.5"
      - signal: "detection.heuristic.ambiguous"
        value: true
    skip_when:
      - signal: "detection.early_exit"
      - signal: "budget.exhausted"

# Budget Constraints
budget:
  max_duration: "00:00:00.100"       # TimeSpan (100ms)
  max_tokens: null                   # null = no limit
  max_cost: null                     # decimal cost limit

# Concurrency Configuration
lane:
  name: "fast"                       # Lane grouping for semaphore pools
  max_concurrency: 8                 # Max parallel executions in lane
  priority: 100                      # Higher = runs earlier

# Evidence Requirements
evidence:
  requirements: null                 # Optional expression (e.g., "SNR > 10dB")

# Configuration Bindings
config:
  bindings:
    - config_key: "EnableUserAgentDetection"
      skip_if_false: true
    - config_key: "UserAgentPatternSource"
      maps_to: "pattern_source"

# Tags for filtering/grouping
tags:
  - "fast-path"
  - "header-analysis"
  - "stage-0"

# Metadata
meta:
  author: "scott@mostlylucid.net"
  created: "2025-01-08"
  updated: "2025-01-08"
```

---

## Molecule Manifest Schema (*.molecule.yaml)

```yaml
# ==========================================
# MOLECULE MANIFEST - Atom Assemblage
# ==========================================

# Identity
name: "IdentityWave"
version: "1.0.0"
description: "Foundation wave extracting basic image identity signals"

# Taxonomy (aggregated from atoms)
taxonomy:
  primary_kind: extractor            # Primary role of this molecule
  kinds: [sensor, extractor]         # All roles (from constituent atoms)
  determinism: deterministic         # Aggregated: any probabilistic → probabilistic
  persistence: escalatable           # Aggregated: highest level wins

# Scope
scope:
  sink: "docsummarizer.images"
  coordinator: "analysis"
  molecule: "identity"

# Constituent Atoms (in execution order)
atoms:
  - name: "FormatDetector"
    manifest: "format-detector.atom.yaml"
    required: true                   # Molecule fails if this atom fails

  - name: "DimensionExtractor"
    manifest: "dimension-extractor.atom.yaml"
    required: true

  - name: "AnimationDetector"
    manifest: "animation-detector.atom.yaml"
    required: false                  # Optional - molecule continues if this fails

  - name: "HashGenerator"
    manifest: "hash-generator.atom.yaml"
    required: true

# Aggregate Signal Contract
emits:
  on_complete:
    - key: "identity.format"
      type: string
      source_atom: "FormatDetector"
    - key: "identity.width"
      type: int
      source_atom: "DimensionExtractor"
    - key: "identity.height"
      type: int
      source_atom: "DimensionExtractor"
    - key: "identity.is_animated"
      type: bool
      source_atom: "AnimationDetector"
    - key: "identity.frame_count"
      type: int
      source_atom: "AnimationDetector"
    - key: "identity.sha256"
      type: string
      source_atom: "HashGenerator"
    - key: "identity.dhash"
      type: string
      source_atom: "HashGenerator"

# Dependencies (union of all atom dependencies)
listens:
  required:
    - "input.file_path"
  optional:
    - "config.max_frames"

# Escalation (molecule-level)
escalation:
  to_vision_llm:
    when:
      - signal: "identity.format"
        condition: "IsNullOrWhiteSpace"
    description: "Fallback to LLM if format detection fails"

# Lane (molecule executes as unit)
lane:
  name: "metadata"
  max_concurrency: 4
  priority: 100                      # Foundation - runs first

# Tags
tags:
  - "foundation"
  - "fast"
  - "required"

# Execution semantics
execution:
  mode: sequential                   # sequential|parallel|pipeline
  fail_fast: true                    # Stop on first required atom failure
  timeout: "00:00:30.000"            # Molecule-level timeout
```

---

## Coordinator Profile Schema (*.profile.yaml)

```yaml
# ==========================================
# COORDINATOR PROFILE - Execution Context
# ==========================================

name: "SingleRequest"
description: "API/UI single-request processing with 30s timeout"

# Scope
scope:
  sink: "docsummarizer.images"
  coordinator: "analysis"

# Timeout
timeout: "00:00:30.000"

# Lane Configuration
lanes:
  metadata:
    max_concurrency: 4
  ocr:
    max_concurrency: 1
  llm:
    max_concurrency: 1
  gpu:
    max_concurrency: 1

# Enabled Molecules (subset for this profile)
molecules:
  enabled:
    - "IdentityWave"
    - "ColorWave"
    - "AutoRoutingWave"
    - "MlOcrWave"
    - "VisionLlmWave"
  disabled:
    - "ClipEmbeddingWave"            # Too slow for single request

# Budget
budget:
  max_duration: "00:00:30.000"
  max_cost: 0.10                     # 10 cents per request
```

---

## Signal Naming Conventions

### Hierarchical Namespace

```
{scope.sink}.{scope.coordinator}.{scope.atom|molecule}.{signal_name}

Examples:
- botdetection.detection.useragent.confidence
- docsummarizer.images.analysis.identity.format
- ephemeral.atom.sensor.output
```

### Standard Signal Prefixes

| Prefix | Purpose | Examples |
|--------|---------|----------|
| `atom.*` | Atom lifecycle | `atom.started`, `atom.completed`, `atom.failed` |
| `molecule.*` | Molecule lifecycle | `molecule.started`, `molecule.completed` |
| `detection.*` | Bot detection results | `detection.confidence`, `detection.bot_type` |
| `identity.*` | Image identity | `identity.format`, `identity.width` |
| `ocr.*` | Text extraction | `ocr.text`, `ocr.confidence` |
| `vision.*` | Vision model output | `vision.caption`, `vision.entities` |
| `escalation.*` | Escalation events | `escalation.triggered`, `escalation.target` |
| `budget.*` | Budget tracking | `budget.remaining`, `budget.exhausted` |
| `config.*` | Configuration values | `config.enabled`, `config.threshold` |

---

## Mapping: BotDetection Detectors → Atoms

| Detector | Atom Kind | Persistence | Lane | Priority |
|----------|-----------|-------------|------|----------|
| UserAgentDetector | Sensor | Ephemeral | fast | 100 |
| HeaderDetector | Sensor | Ephemeral | fast | 100 |
| IpDetector | Sensor | Ephemeral | fast | 100 |
| SecurityToolDetector | Sensor | Ephemeral | fast | 100 |
| VersionAgeDetector | Extractor | Escalatable | fast | 95 |
| ClientSideDetector | Sensor | Escalatable | fast | 90 |
| BehavioralDetector | Proposer | Escalatable | behavioral | 80 |
| InconsistencyDetector | Constrainer | Ephemeral | analysis | 70 |
| HeuristicDetector | Proposer | DirectWrite | ml | 60 |
| LlmDetector | Proposer | DirectWrite | llm | 50 |

### Detector Pipeline as Molecule

```yaml
name: "FastPathPipeline"
atoms:
  - UserAgentSensor
  - HeaderSensor
  - IpSensor
  - SecurityToolSensor
execution:
  mode: parallel                     # All stage-0 detectors run in parallel

name: "FullAnalysisPipeline"
atoms:
  - FastPathPipeline                 # Molecule can contain molecules
  - VersionAgeExtractor
  - ClientSideSensor
  - BehavioralProposer
  - InconsistencyConstrainer
  - HeuristicProposer
  - LlmProposer
execution:
  mode: pipeline                     # Sequential with dependency resolution
```

---

## Mapping: DocSummarizer Waves → Molecules

| Wave | Molecule Name | Constituent Atoms |
|------|---------------|-------------------|
| IdentityWave | IdentityMolecule | FormatSensor, DimensionExtractor, AnimationSensor, HashExtractor |
| ColorWave | ColorMolecule | PaletteSensor, SaturationExtractor, DominantColorRanker |
| AutoRoutingWave | RoutingMolecule | QualityProposer, RouteConstrainer |
| MlOcrWave | OcrMolecule | OpenCvSensor, Florence2Extractor, TextFusionRanker |
| VisionLlmWave | VisionMolecule | VisionLlmProposer |
| Florence2Wave | CaptionMolecule | Florence2Proposer |
| MotionWave | MotionMolecule | OpticalFlowSensor, MotionExtractor |
| ClipEmbeddingWave | EmbeddingMolecule | ClipEmbedder |

---

## Implementation Plan

### Phase 1: Core Manifest Infrastructure (ephemeral library)

1. **AtomManifest.cs** - Deserialization model for atom YAML
2. **MoleculeManifest.cs** - Deserialization model for molecule YAML
3. **CoordinatorProfile.cs** - Deserialization model for profile YAML
4. **ManifestLoader.cs** - Loads from embedded resources or file system
5. **ManifestValidator.cs** - Validates signal references, dependencies
6. **SignalContractBuilder.cs** - Builds runtime contracts from manifests

### Phase 2: BotDetection Migration

1. Create `*.atom.yaml` for each detector (10 files)
2. Create `fast-path.molecule.yaml` for stage-0 pipeline
3. Create `full-analysis.molecule.yaml` for complete pipeline
4. Update `EphemeralDetectionOrchestrator` to use manifest loader
5. Add manifest-driven signal emission

### Phase 3: DocSummarizer Migration

1. Convert existing `*.wave.yaml` to new schema (atom + molecule split)
2. Create atom manifests for individual analyzers
3. Update `WaveManifestLoader` to use shared `ManifestLoader`
4. Unify signal emission patterns

### Phase 4: Convergence

1. Extract shared manifest infrastructure to `mostlylucid.ephemeral.atoms.taxonomy`
2. Both projects reference shared manifest system
3. Consistent signal contracts across all atoms/molecules
4. LLM-consumable contract summaries for dynamic composition

---

## Manifest Packaging System

Manifests reference NuGet packages for implementations (inverted from typical package-contains-manifest pattern).

### Key Principle: Manifests Reference Packages

```yaml
# In atom manifest
implementation:
  nuget:
    package: "Mostlylucid.BotDetection.Detectors"
    version: "^2.0.0"
    type: "Mostlylucid.BotDetection.Detection.Detectors.UserAgentDetector"
```

This enables:
- **Dynamic composition** via signal mesh
- **Hot reload** of manifests without rebuild
- **Premium packages** on private registries
- **Independent versioning** of contracts vs implementations

### Signal-Based Composition

Signals create automatic dependency discovery:

```
UserAgentSensor ──emits──> detection.useragent.* ──listens──> InconsistencyConstrainer
```

At runtime, the system can:
1. Build dependency graph from signal contracts
2. Validate all required signals have providers
3. Topologically sort for execution order
4. Dynamically add compatible manifests

See `MANIFEST-PACKAGING.md` for complete details on:
- Package reference patterns (direct type, interface, factory)
- Manifest distribution (Git, HTTP, NuGet)
- Lock files for reproducible builds
- Self-hosted registries
- Version resolution with SemVer

---

## Benefits

1. **Universal Contract**: Every entity (atom, molecule, coordinator) has declarative YAML contract
2. **Signal-Driven Composition**: Dependencies expressed as signals, not code references
3. **LLM-Friendly**: Contracts are structured data for AI orchestration
4. **Testable**: Contracts can be validated statically
5. **Discoverable**: All signals documented in manifests
6. **Convergent**: Same patterns for BotDetection and DocSummarizer
7. **Portable**: Atoms work anywhere if signal dependencies are satisfied
8. **Dynamic**: Manifests reference packages, not vice versa
9. **Plugin-Ready**: Premium packages on private registries