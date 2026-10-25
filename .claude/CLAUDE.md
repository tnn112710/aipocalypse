# CLAUDE.md

Project guidance is shared with other agents and lives in the root `AGENTS.md`. Edit that file, not this one.

@../AGENTS.md

## Claude Code only

- A SessionStart hook (`.claude/hooks/check-undeclared-packs.sh`) reports vendor pack folders missing from ASSETS.md / PLUGINS.md. When it reports some, raise them with the user first and follow its instructions.
- Skills are discovered through `.claude/skills`, a symlink to `.agents/skills/`. If skills don't load, check that the symlink survived checkout (`git config core.symlinks true`, Windows Developer Mode).
- **Images go to Codex, code stays with Claude.** You write all code, scene/prefab wiring and importing. When a task needs a *generated image* (concept art, textures, sprites, icons, UI art or mockups, reference sheets), hand the generation to Codex automatically if it's available: `codex login status` exits 0 (`/codex:setup` checks it too). Use the `codex:codex-rescue` agent with a prompt that names the image tool, the size/format/transparency, the style, and the exact absolute save path. Then open the file yourself to check it, and re-prompt Codex if it's wrong. If Codex isn't installed or logged in, tell the user rather than falling back silently.
  - Save to the session scratchpad for drafts and to `Assets/_Placeholder/<Kind>/` for anything Unity should import (AI-generated, never shipped, see AGENTS.md → `_Placeholder`). Import settings, materials and references stay your job.
  - This doesn't replace asset packs: check ASSETS.md first, and generate only when no declared pack covers the need or the user asks for it.
