# US4 Shared Files Evidence

- Topology: Gluster `replica 3 arbiter 1`
- Full data bricks: node-1 and node-2
- Arbiter: node-3
- Shared mount: `/srv/massar-shared` on all nodes
- Application mount: backend and worker use the same shared application-data
  location
- Peer state: all connected
- Heal backlog: zero
- Split-brain entries: zero
- Restore sentinel: present with matching checksum from all three nodes
- Backend durable writes now use one shared storage contract with normalized
  roots, symlink/traversal refusal, temporary writes, SHA-256, fsync, atomic
  publication and cleanup.
- Content images, lesson resources, question media, sales assets, student
  audio and live-support attachments were moved to the shared mount contract.
- Worker subtitle and mind-map paths use the shared mount and atomic writes.
- Backend storage integration tests and worker path/atomic-write tests passed.

The internal three-node Garage repository was initialized, an encrypted Restic
backup succeeded, and node-3 restored a checksum-known sample into an isolated
directory with a matching digest. Backup/restore timers are active on all
three nodes. The accepted node-1 drill resolved and blocked the actual dynamic
brick port from Gluster status (`56193`), rather than assuming the obsolete
`49152-49251` range. Direct isolation was observed in 2.25 seconds, writes
continued with zero acknowledged loss and zero client-visible outage, the
brick recovered in 2.18 seconds, heal completed with no split brain, and no
recovery marker remained. The post-drill three-node status passed, so T079 is
complete. The storage implementation is included in the running immutable release
`src-0541078d8f68c5f05df6cf21f665e6714390d4e4`.
