# Changelog

## 0.5.0 - 2026-10-28

- One-time top-up credits never expire. Monthly subscription credits expire at UTC month end and are used first. Account and provider controls distinguish both balances and pending usage.
- Added an Atlas credits option in provider settings and the chat settings menu, with monthly and one-time pricing, balance, pending usage, purchase links, and selection of available OpenAI or Claude models.
- Credit setup explains when a saved personal API key takes precedence and links to its management controls. Existing keys are never removed by selecting a model.
- The account window now shows credits without requiring an existing managed plan, refreshes on return from the browser, and distinguishes unavailable account data from an empty balance.

## 0.4.0 - 2026-10-09

Changes since 0.3.0:

### Added

- A separately packaged Atlas AI Connector for Codex and Claude Code, with a bundled MCP server, the build/diagnose/review skills, and setup instructions for local desktop and CLI sessions. It uses the existing Atlas sign-in and product access in Unity.
- Connector setup verifies the release files, registers the local MCP server through the selected client's CLI, and installs the three skills. Manual MCP setup is available when the client CLI is not on PATH.
- Claude Code can run Atlas requests on your own computer, alongside local Codex. Atlas starts the Claude Code program you installed, with the sign-in you set up in it, and never reads or uploads its credentials. Requests are billed to your own account by Anthropic or your cloud provider.
- A Sign In button for Claude Code in Atlas Providers, and the same action on the chat message that appears when it is not signed in. Both open Claude Code's own sign-in in a terminal and notice when it finishes. Signing in to the Claude desktop app does not sign in the command-line program it installs.
- A Run with choice for Claude Code: Auto, the Claude desktop app's own installation, or a CLI you point at.
- Open the Atlas manual from the sign-in screen, chat settings, or Tools > Opsive > Atlas > Documentation. Development clients use the staging manual.
- The composer uses a slim paperclip attachment icon. Provider-supported text, image, document, video, and audio files can be attached; unsupported files are reported before sending.
- Atlas can use a bounded set of Unity CLI scene editing commands when the per-project scene-editing setting is enabled and the user explicitly asks it to change the project. Read-only inspection remains on by default when dependencies are installed. Code changes continue through Atlas's validated generation flow; arbitrary evaluation and general-purpose file tools remain unavailable.

### Changed

- The model dropdown groups providers by where a request runs: Local for a runtime on this computer, API for one Atlas reaches for you.
- Auto now prefers a runtime installed on this computer, Codex first and then Claude, and only falls back to the providers Atlas can reach. A first run no longer pins one local provider, so a computer with only one of them still works without changing the model.
- A prompt that says to create something generates straight away. The generate-or-answer step remains for prompts where generating was inferred rather than asked for.
- The chat model dropdown chooses the active provider and model. Atlas Providers shows settings without switching the chat model.
- Messages about unavailable Atlas-managed access point at the Local group rather than naming one runtime.

## 0.3.0 - 2026-09-28

Changes since 0.2.2:

### Added

- Bring your own OpenAI or Claude API key alongside the local Codex option. Every signed-in Atlas account can configure a key without a separate BYOK enablement step.
- A provider dropdown in Atlas Providers that shows settings for only the selected route. Atlas selects local Codex by default when it finds an installed runtime; an explicit provider choice is retained.
- Optional Unity CLI integration for local Codex on Unity 6 and newer. When Unity CLI and Unity Pipeline are installed, Atlas uses allowlisted read-only tools to inspect the live Editor's scenes, assets, selection, and settings by default. Without them, prompts continue normally. The integration does not edit the project, run tests or builds, or enter Play Mode.

### Changed

- Account and provider settings now use UI Toolkit throughout, with clearer key, subscription, and runtime guidance.
- Atlas checks the selected route before sending a prompt and gives actionable errors for an unavailable local Codex runtime or a missing or unreadable saved API key.
- Atlas Providers names the missing Unity CLI, Unity Pipeline package, or Unity version when a user explicitly tries to enable Unity inspection; normal prompts still work without those tools.
- Strengthened isolation and recovery for local Codex sessions that use Unity inspection.
- Improved text rendering in the Atlas window on Unity 6 and clarified the welcome message.

### Fixed

- Improved handling when a selected provider or model becomes unavailable after a model-catalog refresh.
- Unity inspection no longer exposes general-purpose file reads or searches. File-based Codex sign-in remains isolated without copying credentials; other sign-in modes are checked within the isolated session before tools are exposed. Isolation failures provide a clear remedy.
