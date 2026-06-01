# Research: Google Drive Video Provider

No foundational uncertainties or `NEEDS CLARIFICATION` markers exist for this feature. The technology stack, abstraction patterns, and security mechanisms are already well-established.

## Decisions

- **Decision 1**: The Google Drive embed player will be rendered via a standard iframe initialized by the `embed/route.ts` backend within a Shadow DOM.
  - **Rationale**: Reuses the core architecture applied for YouTube and Telegram providers. Hiding the `fileId` via Shadow DOM ensures students cannot scrape the DOM easily.

- **Decision 2**: `google_drive` will be added as valid literal for the `provider` field enum/string. 
  - **Rationale**: Minimal schema changes. Fits the existing `VideoProviderAbstraction` framework.
