# AGENTS.md

# Unity AI Coding Rules

This file defines how AI coding agents should work in this Unity project.

The goal is not to maximize code generation.
The goal is to produce simple, maintainable, production-ready code while preserving the existing architecture.

---

# 1. Core Principles

## 1.1 Understand Before Changing

Before modifying code:

1. Identify the scripts related to the task.
2. Read the existing implementation.
3. Understand the current data flow.
4. Identify existing systems that can be reused.
5. Check how the feature is currently connected to other systems.

Do not immediately write code based only on the user's description.

Prefer understanding the existing codebase over inventing a new solution.

---

## 1.2 Preserve Existing Architecture

The existing architecture is the default.

Before creating a new:

- Manager
- System
- Service
- Event
- EventBus
- Interface
- State Machine
- Singleton
- Utility class

check whether an existing solution already exists.

Do not introduce a new architecture just because another architecture is theoretically cleaner.

Use the smallest change that correctly solves the problem.

---

## 1.3 Do Not Over-Engineer

Prefer:

Simple solution
over
Abstract solution

Small change
over
Large refactor

Existing system
over
New system

Readable code
over
Clever code

If a feature can be implemented with 20 lines without creating unnecessary dependencies, do not create a 200-line framework.

---

# 2. Task Workflow

For every task, follow this workflow.

## Step 1 — Explore

Find the relevant files and inspect the current implementation.

Do not modify code yet.

Determine:

- Which scripts are involved?
- Which system owns the responsibility?
- What existing methods can be reused?
- What dependencies exist?
- What assumptions are being made?

---

## Step 2 — Explain

Before implementing a non-trivial feature, briefly explain:

- How the current system works.
- Where the new behavior should live.
- Why that location is appropriate.

If the requested change is trivial and isolated, implementation may proceed directly.

---

## Step 3 — Plan

For non-trivial tasks, provide a short implementation plan.

The plan should contain:

1. Files to modify
2. What will change
3. Existing systems to reuse
4. Potential risks
5. Edge cases

Do not create unnecessary files.

If the plan requires significant architectural changes, stop and ask for approval.

---

## Step 4 — Implement

Implement only the required changes.

Rules:

- Do not modify unrelated code.
- Do not rewrite entire files unnecessarily.
- Do not rename public APIs without permission.
- Do not silently change behavior outside the requested feature.
- Reuse existing systems whenever possible.
- Keep the diff small.

---

## Step 5 — Verify

After implementation, verify the changes.

Check for:

- Compilation errors
- NullReferenceException
- Incorrect Unity lifecycle usage
- Incorrect state transitions
- Event subscription/unsubscription problems
- Missing references
- Serialization problems
- Incorrect physics behavior
- Unnecessary allocations
- Performance problems

If tests or builds are available, run the relevant ones.

Never assume code works simply because it compiles.

---

## Step 6 — Review

Review the final implementation as a senior Unity developer.

Look for:

- Logic bugs
- Edge cases
- Unnecessary complexity
- Coupling
- Performance problems
- Memory/GC problems
- Maintainability issues
- Potential regressions

Report important problems instead of silently hiding them.

---

# 3. Requirements and Assumptions

Never silently guess important requirements.

If something is unclear:

1. Check the existing code for clues.
2. If the answer is still unclear, state the assumption.
3. For important architectural decisions, ask before implementing.

Do not invent:

- Gameplay behavior
- Data structures
- APIs
- Events
- Managers
- Dependencies

unless necessary.

---

# 4. Unity Rules

## 4.1 Unity Lifecycle

Be careful with:

- Awake
- OnEnable
- Start
- Update
- FixedUpdate
- LateUpdate
- OnDisable
- OnDestroy

Do not move logic between lifecycle methods without understanding the consequences.

Physics-related logic should use the appropriate physics lifecycle.

---

## 4.2 References

Prefer cached references for frequently accessed components.

Avoid repeatedly doing expensive lookups such as:

```csharp
GetComponent<T>()
FindObjectOfType<T>()
FindObjectsOfType<T>()
GameObject.Find()
```

inside Update, FixedUpdate, LateUpdate, or any per-frame / per-collision code path.

Preferred order:

1. `[SerializeField]` reference assigned in the Inspector.
2. Cached in `Awake()` and reused afterwards.
3. Runtime lookup — only as a last-resort safety net.

When adding a new `[SerializeField]` reference, also fill it in `Reset()` where it can be resolved automatically, so existing prefabs and scene objects do not silently end up with null references.

---

## 4.3 Prefabs, Scenes and Serialization

- Changing a serialized field name breaks existing data. Use `[FormerlySerializedAs]` if a rename is required.
- Removing or reordering serialized fields can silently reset values on prefabs and scene objects. State this risk before doing it.
- Do not change prefab or scene assets unless the task requires it. Report which prefabs/scenes must be updated by hand.
- Data that designers tune belongs in ScriptableObjects or serialized fields, not hard-coded constants.

---

## 4.4 Performance (Mobile)

This is a mobile project. Performance is a requirement, not an optimization pass.

- No per-frame allocations: avoid LINQ, `foreach` over interfaces, string concatenation, closures, and new collections in Update/FixedUpdate.
- Reuse buffers and use the non-allocating physics APIs (`Physics.OverlapSphereNonAlloc`, `RaycastNonAlloc`, etc.).
- Prefer object pooling over `Instantiate`/`Destroy` for anything spawned repeatedly.
- Prefer `sqrMagnitude` over `magnitude` for distance comparisons.
- Do not add an `Update()` to a component that does not need one; drive many objects from one manager loop when it is simpler.
- When raising a performance concern, back it with a real measurement (Profiler, frame time, allocation count) — do not guess numbers.

---

## 4.5 Events and Cleanup

- Every subscription needs a matching unsubscription (`OnEnable`/`OnDisable`, or `Awake`/`OnDestroy` — pick one pair and stay consistent).
- Coroutines started on a disabled or destroyed object stop silently. Do not rely on them for cleanup.
- Static state and singletons survive Play Mode reloads in the Editor. Reset them explicitly.

---

# 5. Communication

- Report what was changed, file by file.
- Report what was not done, and why.
- Report risks, assumptions, and anything left to verify in the Editor.
- If something unexpected shows up mid-task, stop and ask instead of working around it silently.
- Never report a task as finished when only part of it is done.
