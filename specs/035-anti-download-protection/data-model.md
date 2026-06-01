# Anti-Download DRM & IDM Protection Data Model

This feature implements DOM obfuscation and proxy logic within the Next.js `embed` system. There are no new internal or external database tables required to handle IDM blocking, as the proxy strictly evaluates incoming HTTP request headers.

## State Management

Instead of persistent databases, the proxy relies on in-memory cryptographic masking:
*   `cdnUrl` is wrapped in an AES-256-GCM symmetric encryption token `t`.
*   The token is passed directly on the DOM URL and decoded losslessly in the proxy. Null state is achieved if decryption fails (e.g., tampered padding or mismatched Next.js secret).
