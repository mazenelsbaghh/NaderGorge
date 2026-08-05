# Phase 1 baseline

The inventory and posting matrix classify the selected authoritative sources. No historical row is posted by inference: rows that cannot be tied to a wallet/source identity are returned as migration ambiguities. Baseline SQL is read-only and ready to run against a disposable or read replica.

Go/no-go for local schema work: **go for the additive ledger and read-only cockpit**. Production mutation cutover remains gated on PostgreSQL integration, historical dry-run evidence, and the three-node migration gate.
