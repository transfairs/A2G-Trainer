---
name: pre-release-check
description: Runs the full pre-release readiness pass for A2G-Trainer-XP, closing test-coverage gaps to 100% (including designer-generated code), filling missing public-member doc comments, syncing the Cheat Engine table / Readme / in-app help / online help with the current code, and verifying build + changelog scripts (including that the app's About-screen version label still reads from the assembly version rather than a hardcoded string). Never commits, stages, pushes, or tags; all changes are left in the working tree for manual review. Use before cutting a new release (before creating/pushing a `vX.Y.Z` tag).
---

# Pre-Release Check

A release is cut by pushing a `vX.Y.Z` tag, which triggers `.github/workflows/release.yml`
(build, test, package, publish). This skill is everything that should happen *before* that:
run it, review its report and the resulting diff, then decide yourself when to commit and tag.

**Hard rule: never run `git add`, `git commit`, `git push`, or create/push a tag as part of this
skill.** Every fix this skill makes must be left as an uncommitted (or unstaged) change in the
working tree. Report what you changed; the user commits and tags manually.

Track progress through the phases below with TodoWrite; this is a long, multi-part pass and
losing track of which phase is done is the main failure mode.

Before starting, run `git status` and `git log --oneline -1` so you know the starting state and
can accurately describe what changed by the end.

---

## Phase 1: Auto-fix (the skill actively closes gaps)

Do these in order; later phases assume earlier ones are done (e.g. the doc-comment pass should
see the final code, so do it after any code touched by other phases).

### 1.1 Test coverage → 100%, including generated Designer.cs code

The build/test toolchain is unusual for this repo, so follow the README's "Testing" section
exactly, don't use plain `dotnet build`/`dotnet test`:

```
msbuild A2G-Trainer-XP.Tests/A2G-Trainer-XP.Tests.csproj /t:Restore,Build /p:Configuration=Debug
dotnet test A2G-Trainer-XP.Tests/A2G-Trainer-XP.Tests.csproj --no-build --no-restore --collect:"XPlat Code Coverage"
reportgenerator -reports:"A2G-Trainer-XP.Tests/TestResults/**/coverage.cobertura.xml" -targetdir:coveragereport -reporttypes:"Html;Cobertura"
```

`msbuild` must resolve to full Visual Studio MSBuild, not the dotnet-SDK one (see README note).
If `coverlet.collector` isn't already a package reference in the test project, add it first
(`dotnet add A2G-Trainer-XP.Tests package coverlet.collector`), which is an uncommitted csproj
change like everything else here. If `reportgenerator` isn't installed, install it
(`dotnet tool install -g dotnet-reportgenerator-globaltool`).

Loop:
1. Read the Cobertura XML (or the Html report) to find uncovered lines/branches, **across every
   file in the main project, including `*.Designer.cs`**, since there is no exclusion for generated
   UI code in this repo's target.
2. For each gap, write a focused xUnit test in `A2G-Trainer-XP.Tests` that exercises it. Match
   existing patterns in the test project (e.g. the STA-thread WinForms setup in
   `StaThread.cs`/`PlayerViewTests.cs` for constructing views and hitting `InitializeComponent`
   and designer-wired event handlers; the self-attach pattern in
   `ProcessMemoryIntegrationTests.cs` for memory code).
3. Re-run the build+test+coverage sequence and check again.
4. Repeat until the report shows 100%.

Always state the resulting coverage percentage explicitly in your output to the user — even if no
gaps were found and it was already 100% before this run. This is a mandatory line in every run of
this skill, not just when something needed fixing.

Escape hatch: if you hit code that is genuinely unreachable by any test (e.g. a defensive
`default:` branch that can't be triggered, dead code): do **not** delete it, don't add
`[ExcludeFromCodeCoverage]` to make the number look right, and don't fabricate a test that
doesn't meaningfully exercise it. Stop and report the specific line(s) to the user for a decision
instead of silently working around it or looping forever.

### 1.2 Doc comments on every public member

Scan the main project (`Model/`, `Controller/`, `View/`) for public classes, methods,
properties, and fields that lack a descriptive comment (the existing convention is XML `///`
summary comments; see e.g. `Model/Addresses.cs`, `Model/AddressPresets.cs`). Add a comment for
each one you find missing. Keep them factual/descriptive (what it is/does), matching the tone of
existing doc comments already in the file; don't invent behavior you haven't verified by reading
the implementation.

### 1.3 Cheat Engine table sync (`A2G-Trainer-XP.ct`)

`Model/AddressPresets.cs` (and `Model/Addresses.cs`) is the source of truth for reverse-engineered
hex offsets, keyed by enum (e.g. `PlayerEnums.AddressKey`, club/coach equivalents). The `.ct` file
mirrors these as `CheatEntry` pointer chains (`<Address>anstoss2.exe+...</Address>` +
`<Offsets>` list) grouped under `GroupHeader` sections (e.g. "Verein").

- Diff the offset keys defined in code against the entries present in the `.ct` file.
- For every field that exists in code but has no corresponding entry, add one, **always** as a
  pointer (`Address` + `Offsets`, matching the existing style), never a bare static address.
- Insert the new entry into the correct existing `GroupHeader` group, next to related fields
  (don't just append at the end of the file).
- Only add entries you can map to a real offset in `AddressPresets.cs`/`Addresses.cs`; don't
  guess values.

### 1.4 Readme.md

Compare `Readme.md`'s Features/Tips/Testing sections against the actual current behavior of the
code (and against what changed since the last tag, via `git log <last-tag>..HEAD`). Update wording
for anything new, changed, or removed. Keep the existing tone/format (emoji bullet list, English).

### 1.5 In-app help (`View/HelpView.cs`)

The RTF blocks in `InitHelpTexts()` are German. Check each section (Verein/Spieler/
Jugendspieler/Trainer & Liga/Tipps/FAQ-equivalent) against current app behavior and update or add
content for anything new/changed, in German, matching the existing RTF formatting conventions
(`\b`, `\qj`, colors already defined in the color table); don't introduce new RTF constructs.

### 1.6 Online help (`help/index.html`)

Same content scope as 1.5 but as the standalone page deployed via `.github/workflows/pages.yml`.
Keep it in sync with `HelpView.cs`; the two should describe the same features. Match the
existing German copy and HTML structure/style already in the file.

### 1.7 `.gitignore`

Run `git status --porcelain=v1 --ignored=matching` and look for directories/files that are
**untracked but not ignored** (`??`, not `!!`) even though they're clearly build/tool output
rather than source, e.g. `dist/` (release script output) and `coveragereport/` (from 1.1's
`reportgenerator` run), plus anything else generated by `scripts/*.ps1` or by this skill's own
runs that isn't already covered by the existing Visual-Studio-boilerplate `.gitignore`. Add
missing entries. Don't remove or restructure existing rules that already work; this is additive.

Also run `git ls-files` and check whether anything currently *tracked* matches a `.gitignore`
pattern (i.e. was committed before it should have been ignored, or an overly broad new rule you're
about to add would newly match a tracked file). Don't `git rm --cached` it yourself; report
it in Phase 3 for the user to decide, since untracking a shipped file is a judgment call, not a
mechanical fix.

---

## Phase 2: Automated, no manual step (skill runs these itself; it can't repair a genuine
failure here, only report it)

- **2.1 Full test suite green**: already run in 1.1's loop; if any test other than a coverage gap
  is failing, stop and report it (don't attempt a fix beyond what 1.1 covers).
- **2.2 Both build configs**: `msbuild A2G-Trainer-XP.csproj /restore /t:Rebuild /p:Configuration=Release /p:Platform=x86`
  and the same with `/p:Configuration=Release2007CD`. Both must succeed.
- **2.3 `scripts/Convert-CtToGog.ps1`**: dry run against the current `.ct`
  (`./scripts/Convert-CtToGog.ps1 -InputPath "A2G-Trainer-XP.ct" -OutputPath "<scratch>/dry-run-GOG.ct"`,
  writing to a scratch path, not `dist/`) and confirm it completes without error.
- **2.4 Changelog scripts dry run**: `scripts/Generate-Changelog.ps1` needs a tag; run it with
  `-Tag` set to the latest existing tag (`git describe --tags --abbrev=0`) against a scratch
  `-OutputDir`, then `scripts/New-ChangelogHtml.ps1` against that scratch output, and confirm both
  complete without error. This only catches format regressions in the scripts; it doesn't
  represent the real release's changelog content.
- **2.5 About-screen version label isn't hardcoded**: `View/AboutView.cs`'s `SetVersionLabel()`
  must read `VersionLabel.Text` from the assembly's `AssemblyInformationalVersionAttribute`
  (which the release pipeline stamps from the git tag into `Properties/VersionInfo.cs`), not a
  literal string — this is what keeps the "Über" screen (Hilfe menu) from going stale without any
  manual/skill step. Grep `AboutView.Designer.cs` for a `VersionLabel.Text = "..."` literal; if one
  has been reintroduced, that's a regression, restore the reflection-based read instead of just
  updating the literal's value.

---

## Phase 3: Report only, human decides (do not attempt to fix these automatically)

- **3.1 Git state**: is the tree clean apart from this skill's own edits, on `master`, up to date
  with `origin/master`? Report divergence, don't act on it.
- **3.2 Commit message quality**: list `git log <last-tag>..HEAD --no-merges --oneline` and flag
  any subjects that would read badly as a changelog bullet (e.g. `wip`, `fix`, `asdf`,
  `更新`/placeholder text). Suggest better wording per commit if useful, but do not rewrite git
  history; that's the user's call.
- **3.3 Screenshots**: list `docs/*.png` and cross-check against UI-affecting files changed since
  the last tag (`View/*.cs`, `.Designer.cs`). Flag which screenshots are plausibly stale; don't
  regenerate images yourself.
- **3.4 Version suggestion**: based on the commits since the last tag (features vs. fixes vs.
  breaking changes), propose the next `vX.Y.Z` per semver and explain why. No app-side follow-up
  needed for this one — the About screen's version label reads the assembly's
  `AssemblyInformationalVersion` at runtime (see 2.5), which the release pipeline already derives
  from whatever tag is actually pushed.
- **3.5 Tracked files that should be ignored**: any files found in 1.7 that are currently tracked
  but match a `.gitignore` pattern (existing or newly added). Report them; leave them tracked.

---

## Final report

Summarize, grouped by phase:
- What was changed automatically (list files touched in 1.1–1.7, with a one-line reason each).
- **Test coverage percentage, before and after this run — always stated, even when it was already
  100% and nothing needed fixing.**
- Any Phase-1 items where you hit the escape hatch and stopped for a decision.
- Phase 2 pass/fail per check.
- Phase 3 findings.
- Explicit reminder: nothing was staged, committed, or pushed; review the diff
  (`git status` / `git diff`), then commit and tag manually when ready.
