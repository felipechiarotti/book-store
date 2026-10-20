---
name: dotnet-final-validation
description: Use after a .NET implementation and its code review, before reporting the changed behavior as verified. Do not invoke for read-only reviews or documentation-only edits.
---

# .NET final validation

Verify the exact candidate diff using the repository's SDK, solution, lock files and documented commands. This skill adds a completion gate; it does not replace Superpowers discovery, planning, TDD, review or verification-before-completion.

## Scope and permissions

- Read the repository AGENTS.md and its verification matrix. Preserve existing authorization and user changes.
- Inspect the full candidate diff and distinguish pre-existing issues from regressions. Do not auto-format unrelated files.
- Do not read private environment files, run migrations, start a migration-applying host, send real email, call production, deploy or commit without the corresponding explicit authorization.
- Prefer disposable integration infrastructure already used by the project. Missing infrastructure is a limitation to report, not permission to use the configured application database.

## Review and checks

1. Confirm requirements and independent review findings are resolved. Review correctness, nullability, async/cancellation, races, disposal, DI lifetimes, exception boundaries, deferred/repeated LINQ, bounded EF queries, transaction correctness and API compatibility where affected.
2. Exercise relevant boundaries: empty/missing data, duplicates, repeated execution, concurrent requests, timezones/date limits, timeout/cancellation and dependency failure. Do not generate tests unrelated to the change.
3. Review the changed attack surface: authentication, resource ownership, injection, uploads, sensitive logging/errors, secrets, cryptography, secure randomness and abuse controls where affected. Use a connected security scanner when the change warrants additional analysis; it is not mandatory for every .NET edit.
4. Restore and build the appropriate solution/project. Run targeted tests, then the full relevant suite. Run repository-configured analyzers/format checks and report new warnings. Never treat a build as proof of test execution.
5. Run dependency vulnerability checks, normally dotnet list <solution> package --vulnerable --include-transitive. A command exit code alone does not establish that no vulnerabilities were reported; inspect the results. Review new dependencies separately.
6. For component boundaries, run available database/HTTP integration tests. Report opt-in tests as skipped if their flag/infrastructure is absent. Mocks do not prove persistence or full host startup.
7. For user-visible behavior, exercise the highest useful functional level available. If browser fixtures mock the API, explain that limitation. Do not imply an end-to-end deployed system was tested.

Use repository commands rather than prescribing a universal clean/build or infrastructure setup. If validation exposes a defect, return to diagnosis/TDD and rerun affected checks after the fix; do not alter expected behavior merely to get a green gate.

## Evidence

Invoke superpowers:verification-before-completion. Inspect final git diff/git status and report the validated revision/diff, exact commands, exit codes, targeted/full test outcomes, security findings, skipped checks and functional limitations. Do not claim complete verification when a required gate failed or could not run. No commit, push, merge or deployment is implied by successful validation.
