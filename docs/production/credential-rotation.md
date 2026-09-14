# Credential rotation

Store values only in the root-owned external secret directory with mode 0600.
Inventory, manifests, evidence and commands contain references only.

For credentials that support overlap, create the replacement, grant it, update
one client/node at a time, verify traffic and failover, then revoke the old
value. For a credential without overlap, take a fresh verified backup, drain
one node at a time, use the shortest maintenance window, and test every
dependent path before continuing.

Rotate separately and record evidence for PostgreSQL application,
superuser/replication/rewind, etcd root/Patroni, Redis/Sentinel, JWT and callback
signing, backup repository, Cloudflare Tunnel, WireGuard and operator SSH keys.
Never rotate two quorum members simultaneously. A failed check stops the
rotation and retains the last working credential until recovery is complete.
