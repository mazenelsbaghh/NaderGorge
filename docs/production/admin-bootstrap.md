# First Admin bootstrap

The database intentionally contains no fixed Admin identity. Run the protected
bootstrap only after migrations and the zero-finding schema audit pass.

Use `deploy/production/scripts/bootstrap_admin.py` from a protected operator
terminal. It prompts without echo, requires a digits-only phone and a password
of at least ten characters, hashes the password in memory with BCrypt work
factor 12, and creates the user, Admin role link, and audit record in one
transaction. It refuses duplicate phone numbers and a missing Admin role.

The operator terminal must already have
`ConnectionStrings__DefaultConnection` populated from the external secret
store and routed through a protected SSH tunnel to a node-local PostgreSQL
writer endpoint. The helper does not retrieve a database password from a
server, and it does not accept an Admin password as a command argument.

Never provide the password in chat, command arguments, SQL, shell history,
evidence, or tracked files. After creation, verify Admin login through the
protected rehearsal hostname, change the initial password if policy requires,
and retain only the non-secret audit/user identifier.
