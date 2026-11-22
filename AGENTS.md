# Working in neradio

neradio is a freeware, ad-free internet radio app for Windows and Android. The original brief is
[doc/entrypoint/UserStory.md](doc/entrypoint/UserStory.md); the requirements themselves live in
OpenSpec.

## Specs and changes (OpenSpec)

- Current requirements live in `openspec/specs/`. Every change to them starts as a change folder
  in `openspec/changes/`.
- Workflow: `openspec-explore` to think a problem through → `openspec-propose` for the proposal,
  design, delta specs and tasks → `openspec-apply-change` to implement the tasks →
  `openspec-archive-change` to fold the delta specs into `openspec/specs/`.
- Work is planned as GitHub issues, and each issue gets one or more PRs. An OpenSpec change takes
  at least two: first the proposal alone, for review, then the implementation. The PR that
  finishes the tasks also archives the change, so `openspec/specs/` describes only what's built.
- A proposal's PR marks the open questions it settles as Resolved in `UserStory.md`.

## Writing code

The code's shape and the rules across it are in [doc/dev/](doc/dev/00-overview.md): read the overview and
the principles before adding a project, a service or a test.

Before changing product source, follow the `neradio-surgical-change` skill
([.agents/skills/neradio-surgical-change/SKILL.md](.agents/skills/neradio-surgical-change/SKILL.md)).
The conventions it depends on:

- **Minimalist.** Smallest diff that fully solves the problem, at the true source of the behavior.
  No speculative configuration, abstractions, or extension points.
- **No nested call expressions on one line.** Bind the intermediate to a named local instead.
- **LINQ / lazy sequences and raw arrays over `List<T>`** for arguments and return values that the
  callee does not mutate.
- **Guard only states that can actually occur.** No defensive null-checks or try/catch "just in
  case".
- **Comments only for non-obvious invariants.** Never narrate what the code does. If a signature
  already says it (`static`, a descriptive name), skip the doc comment. Near-identical doc comments
  on sibling methods are boilerplate — cut them.
- **Match the surrounding file**: member grouping and ordering, naming, idiom. Never interleave
  access levels.

## Dependencies

The app is MIT-licensed; these apply from the first design proposal, not just when coding.

- No GPL-licensed dependencies. The audio engine is BASS (#8), which is closed source, and the two
  can't ship in one app.
- Never commit BASS binaries to the repo; fetch them at build time.

## Commits

- Subject prefix by the dominant nature of the change: `(+)` feature · `(*)` behavior change ·
  `(~)` refactor · `(-)` bug fix · `(=)` typo/cosmetic.
- Imperative subject after the prefix, ~50 chars; blank line; body explaining *why*, wrapped ~72
  cols. Body omitted for trivial one-liners.
- Stage only the files belonging to one logical change — never `git add -A` blindly.
- New commits, never `--amend`. Never `--no-verify`, never bypass signing — fix the failing hook
  instead.

## Branches and pull requests

- `git fetch` first, then branch with `git switch --no-track -c drafts/ai/<slug> origin/main` — a
  draft branch must never track `origin/main` (avoids accidental pushes to it).
- Never stack a branch on an unmerged one. If a change genuinely cannot sit on `main`, discuss it
  before branching.
- Each PR gets its own `drafts/ai/<slug>` branch and holds one item of work. A sweep of the same
  mechanical change across many call sites is the exception: one branch and PR for the whole sweep.
- A PR body names each issue it fixes as `Fixes #N`; `Related: #N` is only for issues it leaves
  open.
- A PR that completes a row of #28, the current build stage's plan, says `Related: #28`.
- Never delete a `drafts/ai/*` branch, local or remote, even after it's merged — omit
  `--delete-branch` on `gh pr merge`. Likewise, don't clean up old `git stash` entries; they're kept
  deliberately as recovery points.
- Never `git push`, open or merge a PR, or create a release without being asked first, even if a
  previous push in the same session was approved.

## Verification

- Run the project's canonical CI entry point, not an ad-hoc subset: affected test classes first,
  then the full suite.
- Pin each new behavior and each prevented regression with a test — including assertions that
  something must *not* happen.
- Report results faithfully. If tests fail, show the output; if a step was skipped, say so.

## Cloning on Windows

`.claude/skills/` holds links into `.agents/skills/`. Git checks links out as plain text files
unless they're enabled: turn on Developer Mode and clone with `git clone -c core.symlinks=true`.
On an existing clone, `openspec update` repairs only the `openspec-*` links.
