# Real Codex CLI provider acceptance

Status: blocked / not accepted.

The owner selected Codex CLI for `/admin/ai-agent`. The source supports `ADMIN_AI_PROVIDER=codex-cli` and pins `@openai/codex@0.147.0` with `gpt-5.6-sol` as the default Admin AI model when that provider is selected. The node-3 ChatGPT CLI login remains outside source control and was not printed or copied.

Local synthetic tests passed the actual CLI, including a named read and terminal answer through the Unix-socket sidecar and worker provider client. A separate read-only synthetic invocation succeeded on node 3. These prove the host account/model and local protocol boundary only. They do not prove a deployed container, platform-data answer quality, outbound secret-sentinel capture, or full Admin operation coverage.

The current capability baseline has 659 blocked items and the feature flag remains disabled. Run the production-equivalent real-provider acceptance only after the complete capability, migration, Docker, privacy, and owner gates pass. Record the provider version/model, latency, read and proposed-action outcomes, outbound secret-sentinel result, and zero destructive platform effect here before accepting T209.
