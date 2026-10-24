# CLAUDE.md

Project guidance is shared with other agents and lives in the root `AGENTS.md`. Edit that file, not this one.

@../AGENTS.md

## Claude Code only

- A SessionStart hook (`.claude/hooks/check-undeclared-packs.sh`) reports vendor pack folders missing from ASSETS.md / PLUGINS.md. When it reports some, raise them with the user first and follow its instructions.
- Skills are discovered through `.claude/skills`, a symlink to `.agents/skills/`. If skills don't load, check that the symlink survived checkout (`git config core.symlinks true`, Windows Developer Mode).
