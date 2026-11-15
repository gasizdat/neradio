---
name: surgical-change
description: How to implement a feature or fix with minimal impact - accurate targeting, no excess code, no overengineering. Use before writing any change to product source.
---

# Surgical changes

The goal of every change: smallest diff that fully solves the problem, placed at the true source of the behavior, indistinguishable in style from the surrounding code.

## Target before you type

- Read the exact code path end-to-end first (entry point → logic → storage). Fix where the behavior originates, not where the symptom shows up.
- Before inventing anything, search for existing machinery that already does 80% of it. Prefer wiring into it over building parallel structure.

## Prefer the lighter mechanism

Each of these beats its heavier alternative unless proven insufficient:

- A sentinel/default value over a new type or subclass.
- An in-memory filter over new query/API surface, when the data is already loaded.
- A declarative binding/configuration over imperative manipulation in code (a bound `IsEnabled` beats removing UI items imperatively — and it reverses itself when state changes back).
- Extending an existing method's behavior over adding a parallel method.
- One shared helper when the same rule is needed in two places (keeps both call sites in agreement) — but don't extract helpers for single-use logic.

## No overengineering

- Guard only states that can actually occur. No defensive null-checks, try/catch, or fallbacks "just in case".
- Don't add configuration, abstractions, interfaces, or extension points for hypothetical future needs.
- Hide/skip empty states instead of designing UI for them (an always-present placeholder for a usually-empty group is noise — only show it when non-empty).
- If a change exposes an adjacent latent bug, fix it only when the fix is a line or two on the same seam; otherwise mention it or file an issue.

## Blend in

- Match the file's existing member grouping and ordering (e.g. fields → ctor → public members → private methods); never interleave access levels.
- Follow the coding conventions in `AGENTS.md`: minimalist code, no nested call expressions on one line, LINQ/lazy + raw arrays over List for unchanged args/returns, commit-prefix convention (*)/(+)/(~)/(-)/(=).
- Comments only for non-obvious invariants, never narrating what the code does or why the change is correct.
- Before writing a doc comment, check whether the signature already says it: `static` already says "no instance state needed"; a descriptive method/param name already says what it does. If the comment would just restate that in prose, skip it. A strong tell: if you're about to write near-identical doc comments on two or three sibling methods (e.g. the same "stateless, so callers don't need an instance" paragraph on `Formatter1.Format`, `Formatter2.Format`, `Formatter3.Format`), that's boilerplate, not a real per-method invariant — cut it everywhere, or say it once where it's actually non-obvious.

## Verify like CI does

- Tests: extend the existing test class for the touched component, reusing its harness helpers and mirroring the style of neighboring tests. Pin each new behavior and each prevented regression (e.g. an assertion that a call must NOT happen).
- Build and run the project's canonical CI entry point (solution filter, make target, npm script) rather than an ad-hoc subset; then run the affected test classes first, then the full suite.
- One focused branch and commit per logical change; never push without being asked.
