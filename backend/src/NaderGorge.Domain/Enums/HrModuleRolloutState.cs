namespace NaderGorge.Domain.Enums;

public enum HrModuleRolloutState
{
    Legacy = 1,
    ShadowValidated = 2,
    NewActive = 3,
    RollingBack = 4,
    Failed = 5
}

public enum HrMigrationBatchState { DryRun, Reconciled, Applied, Activated, RolledBack, Failed }
public enum HrMigrationConflictState { Open, Accepted, Resolved, Rejected }
