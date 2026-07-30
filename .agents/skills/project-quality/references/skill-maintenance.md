# Skill Maintenance

## Load this reference when

Adding, consolidating, editing, reviewing, or validating repository skills,
their metadata, progressive references, documentation links, or commands.

## Normative documentation

- [Project structure](../../../../docs/architecture/project-structure.md#project-structure-contract)
- [Namespace and file boundaries](../../../../docs/architecture/project-structure.md#namespace-and-file-boundaries)
- [Documentation ownership](../../../../docs/documentation-contract.md#links-and-ownership)
- [Discovery gate](../../../../docs/testing/correctness-model.md#discovery-gate)

## Repository contract

- `.agents/skills/` is the sole real skill location.
- `.claude/skills` and `.codex/skills` remain relative directory symlinks.
- Six domain skills own stable implementation areas; topic details live one
  level below `SKILL.md` in `references/`.
- Metadata descriptions contain trigger conditions, not workflow summaries.
- Every reference is routed directly from `SKILL.md` and starts with anchored
  normative documentation links.
- References contain navigation, workflows, commands, and traps; product
  behavior remains in `docs/`.

## Workflow

1. Baseline a realistic task without the proposed skill change and record
   routing confusion or missing evidence.
2. Make the smallest metadata, gateway, or reference change that addresses it.
3. Validate links, focused commands, and nonzero discovery.
4. Forward-test the same task with the skill and close concrete gaps.
5. Remove superseded entry points in the same change; do not leave alias skills.

## Mechanical validation

```bash
skill_validator=/Users/alex/.codex/skills/.system/skill-creator/scripts/quick_validate.py
python3 "$skill_validator" .agents/skills/project-quality
```

## Project-specific traps

- Never recreate `docs/superpowers`; internal skill work does not belong in
  normative product docs.
- Do not duplicate general repository coding rules in every skill.
- Do not copy stale APIs or commands from a superseded skill without checking
  the live docs, declarations, tests, and scripts.
