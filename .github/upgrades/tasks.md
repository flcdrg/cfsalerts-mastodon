# Migration Plan: .NET 8.0 → .NET 10.0 Upgrade

## Overview

Big Bang strategy: upgrade both projects in the `CfsAlerts` solution simultaneously from `net8.0` → `net10.0`. Tasks implement the Plan and apply the strategy batching rules: prerequisites separated, project & package updates + compilation fixes combined into a single atomic upgrade task, testing as a separate verification task, final atomic commit.

**Progress**: 3/4 tasks complete (75%) ![75%](https://progress-bar.xyz/75)

## Tasks

### [✓] TASK-001: Verify prerequisites and CI toolchain *(Completed: 2025-11-24 22:35)*
**References**: Plan §3.1 (Phase 0), Plan §7 (Phase 0), Plan §11 (CI/CD Pipeline Updates)

- [✓] (1) Verify .NET 10 SDK is installed locally and available on build agents (e.g., `dotnet --list-sdks` shows `10.*`) per Plan §7 Phase 0.
- [✓] (2) Update CI workflow files to reference .NET 10 (e.g., `actions/setup-dotnet` dotnet-version: `10.0.x`) or ensure build agents include .NET 10 per Plan §11.
- [✓] (3) Verify CI workflow files reference .NET 10 and build agents report .NET 10 available (**Verify**)

---

### [✓] TASK-002: Atomic framework & package upgrade, restore, build and fix compilation *(Completed: 2025-11-24 22:38)*
**References**: Plan §4 (Project-by-Project Migration Plans), Plan §5 (Package Update Reference), Plan §6 (Breaking Changes Catalog), Plan §3.2 (Phase 1)

- [✓] (1) Update `TargetFramework` to `net10.0` in both `CfsAlerts.csproj` and `CfsAlerts.Tests.csproj` per Plan §4.
- [✓] (2) Update package references in `CfsAlerts.csproj` per Plan §5:
      - `Microsoft.Azure.Functions.Worker` → `2.51.0`
      - `Microsoft.Extensions.Configuration.UserSecrets` → `10.0.0`
      (other packages remain unchanged per Plan §5)
- [✓] (3) Run `dotnet restore` for the solution (per Plan §7 Phase 1).
- [✓] (4) Build the solution, identify compilation errors, and apply code fixes per Plan §6 (bounded pass: handle compiler errors reported).
- [✓] (5) Rebuild solution to confirm fixes applied; solution builds with 0 errors (**Verify**)

---

### [✓] TASK-003: Execute automated tests and remediate failures *(Completed: 2025-11-24 22:42)*
**References**: Plan §4 (CfsAlerts.Tests), Plan §3.2 (Phase 2), Plan §6 (Breaking Changes Catalog)

- [✓] (1) Run tests in `CfsAlerts.Tests` (e.g., `dotnet test ./tests/CfsAlerts.Tests/CfsAlerts.Tests.csproj`) after successful build.
- [✓] (2) If tests fail, fix test or product code changes necessary (reference Plan §6 for likely breaking-change patterns) and document fixes (bounded remediation).
- [✓] (3) Re-run tests and verify all tests pass with 0 failures (**Verify**)

---

### [▶] TASK-004: Finalize upgrade: commit, push, and open PR
**References**: Plan §8 (Source Control Strategy), Plan §10 (Review and Merge Process)

- [ ] (1) Commit all changes with task-scoped message: `"TASK-004: Upgrade solution to .NET 10.0"`.
- [ ] (2) Push branch `upgrade-to-NET10` and open a Pull Request to `main` with references to Plan §4, Plan §5, and Plan §6; include checklist: build success, tests passing, CI green (per Plan §10).
- [ ] (3) Verify PR created and CI checks for the PR pass (**Verify**)

