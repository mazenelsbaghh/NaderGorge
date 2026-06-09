# Quickstart: Full Frontend API Contract Audit

1. Regenerate the contract inventory:

   ```bash
   node scripts/generate-endpoint-inventory.mjs
   ```

2. Verify the inventory is current:

   ```bash
   node scripts/generate-endpoint-inventory.mjs --check
   ```

3. Run the focused Python contract tests:

   ```bash
   .venv/bin/python -m pytest tests/test_endpoint_inventory.py
   ```

4. Run project build checks:

   ```bash
   dotnet build backend/NaderGorge.sln
   cd frontend && npm run lint && npm run build
   ```

5. Run Docker configuration validation:

   ```bash
   docker compose config -q
   docker compose ps
   ```

6. Review `tests/endpoint_inventory.md`:

   - Confirm frontend backend calls are grouped by source file.
   - Confirm missing route findings are zero.
   - For any documented exception, confirm it is frontend-local, worker-only, or external and not an ASP.NET backend route gap.
