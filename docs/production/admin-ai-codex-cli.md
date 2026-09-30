# Admin AI Agent through Codex CLI

Status on 2026-09-29: the source integration is local only. It has not been published, deployed, or activated. The Admin AI feature's existing release gates in `specs/169-admin-ai-agent/final-report.md` remain blocked.

## Runtime boundary

- The backend still owns administrator authorization, capability checks, bounded reads, confirmations, execution, and append-only evidence.
- The node-3 worker claims Admin AI jobs and sends bounded provider requests over a local Unix socket. It does not give Codex its database, application secrets, or backend credentials.
- The `admin-ai-codex` sidecar runs the pinned `@openai/codex` CLI in an empty temporary directory with read-only sandboxing and built-in tool surfaces disabled. It uses the existing node-3 ChatGPT CLI login. Codex requests reads by returning JSON; the worker validates each named capability and calls the backend's existing read callback.
- The sidecar has only the restricted repair proxy network, its Codex login directory, and the socket directory. It shares the existing login directory with the auto-repair service, so token renewal and account access remain an operational dependency.

## Activation contract

The source environment selects `ADMIN_AI_PROVIDER=codex-cli`. The environment renderer then fixes `AI_ADMIN_AGENT_RUNNER_NODE=node-3` and `ADMIN_AI_CODEX_SOCKET=/run/admin-ai/codex.sock`; the model defaults to `gpt-5.6-sol`. Other AI worker jobs still use their existing provider. The release helper includes the sidecar only when this provider is selected and checks the login, proxy, network, and socket directory before starting it. The rendered feature flag stays disabled until the complete Admin capability baseline and final acceptance gates pass.

No production activation should happen until the Admin AI feature's blocked capability and verification gates pass. A release also needs the normal shared-source publication, build, migration, rollout, and rollback checks.

## Evidence to date

Local unit and boundary tests exercise the Unix socket, JSONL result parsing, named read requests, forbidden tool events, secrets excluded from the CLI process, and the Admin AI read callback. A synthetic invocation of the actual pinned CLI authenticated locally and completed a named read followed by a terminal answer with `gpt-5.6-sol`. The local ChatGPT login rejected `gpt-6-sol`, so it is not the default. These checks used synthetic data; there has been no production-equivalent provider acceptance test with real platform data.

The same synthetic read and terminal-answer sequence also passed through the running Unix-socket HTTP sidecar and worker provider client with the real CLI. Both requests produced token accounting. The first attempt at this local test used the production-only `/app/node_modules/.bin/codex` default path outside a container and failed; setting `ADMIN_AI_CODEX_BINARY` to the local installed CLI resolved that test setup issue.

A read-only synthetic `codex exec` invocation on node-3 completed with the selected `gpt-5.6-sol` model, the intended sandbox/tool flags, and the existing ChatGPT login. Its prompt contained no platform data. This verifies the host account and model combination, not the unbuilt container or the full Admin AI feature.

The local all-image Docker build is pending: the offline build helper stopped before building because the pinned .NET SDK base image is absent locally. The current generated capability baseline reports `activation=blocked` with 659 blocked items out of 1,125; its inventory tests pass, but that status is a release gate rather than a test failure. The older feature report counted 562 against its earlier baseline.

The 659 blocked items represent 307 distinct authoritative-operation labels in the current inventory. Ninety-four items explicitly require moving direct controller database writes into application commands or services before an Admin AI adapter can be considered safe. The approved full-scope release cannot be declared ready by simply enabling the existing read-only runtime catalog or wiring the provider.

The current runtime registry is built by `CreateProductionReadRegistry()` and contains read capabilities only. There are typed action bridge classes in source, but no production registration of `IAdminAIActionCapability` or implementation of their `IAdminAIActionPreviewSource` dependency. They therefore cannot be counted as executable action coverage. The fresh capability check passes inventory consistency while explicitly reporting `activation=blocked`.
