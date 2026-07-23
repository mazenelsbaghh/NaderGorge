namespace NaderGorge.Domain.Enums;

public enum AttendancePolicyKind { Unrestricted, Geofence, TrustedDevice }
public enum AttendanceEventType { ClockIn, BreakStart, BreakEnd, ClockOut }
public enum AttendanceSessionState { Open, Completed, Corrected, AutoClosed }
public enum AttendanceCorrectionState { PendingManager, PendingHr, Approved, Rejected, Withdrawn }
public enum WorkdayClassificationKind { Workday, Weekend, Holiday, Leave, UnpaidLeave, Absence }
