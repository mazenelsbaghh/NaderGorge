# Query performance evidence

The dashboard and report queries are date-bounded, group by account/source before projection, and use the finance indexes created by the additive migrations. The representative in-memory contract suite completed the dashboard query under 5 seconds for 25 journal entries; production p95 remains environment-specific and must be captured from PostgreSQL with `EXPLAIN (ANALYZE, BUFFERS)` before increasing the release budget.

Required production capture:

```sql
EXPLAIN (ANALYZE, BUFFERS) SELECT ... FROM financial_journal_lines ...;
```

The release gate already verifies that the baseline schema is unchanged outside the target migration tables.
