# OpenSpec Instructions

## Workflow

- Treat `openspec/specs/` as the current source of truth.
- Put proposed behavior under `openspec/changes/<change-name>/`.
- Agree on proposal, delta specs, design, and tasks before implementation.- Archive completed changes so source-of-truth specs match deployed behavior.

## Principles

- Specify verifiable behavior before implementation.
- Keep one focused change per change directory.
- Preserve existing repository intent when translating README, TODO, or prior run artifacts into OpenSpec.

## Validation

- Validate OpenSpec artifacts before review.
- Run repository tests, lint, typecheck, and build only when the requested phase includes validation or implementation.
