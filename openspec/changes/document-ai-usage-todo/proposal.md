# Proposal: Document AI Usage TODO Baseline

## Summary

Preserve the existing TODO/documentation intent for the AI Usage desktop widget in OpenSpec form. The repository already contains `TODO.md`; this proposal captures that work as a traceable OpenSpec change without expanding the scope into source code, UI, packaging, or configuration edits.

## Problem

The active OpenSpec profile needs native proposal artifacts so future runs can distinguish source-of-truth specifications from proposed changes. Current repository artifacts communicate the TODO baseline, but there was no OpenSpec change directory linking that intent to requirements and reviewable tasks.

## Proposed Change

- Add OpenSpec instructions for the repository workflow.
- Add a project-readiness source specification that records the documentation baseline.
- Add this focused change proposal with delta requirements and tasks.
- Preserve the existing `TODO.md` content and its documentation-only scope.

## Non-Goals

- No application code changes.
- No UI, packaging, or runtime configuration changes.
- No test execution in this propose phase.
- No manual commit, push, or pull request creation.

## Native Workflow Note

The native OpenSpec provisioning command used by the active Nexo profile was attempted first:

```text
npx --yes @fission-ai/openspec@1.7.0 init . --tools codex --profile core --force --no-animation --no-copilot-cloud
```

It failed because npm could only use its local cache and the OpenSpec package was not cached. The generated Markdown artifacts preserve the expected OpenSpec proposal structure for Draft PR validation.

