# Bounded production chaos contract

Run only after a green pre-state and only one scenario at a time. Every drill
must capture the current leader/master/heal state, stop exactly one resolved
target, prove continued service and acknowledged data, restore the target, and
wait for full replica/heal/connector convergence before another scenario.

The scenario definitions in `scenarios.json` are acceptance inputs. They do not
authorize arbitrary service stops. PostgreSQL and Redis targets must be resolved
from Patroni/Sentinel immediately before the drill; never infer them from a
fixed node name.
