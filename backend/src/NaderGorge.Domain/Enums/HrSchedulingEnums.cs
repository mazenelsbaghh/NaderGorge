namespace NaderGorge.Domain.Enums;

public enum ShiftTemplateMode { Fixed, Flexible, Rotating, Split }
public enum ShiftWorkDateRule { SegmentStartDate, SegmentEndDate }
public enum ShiftAssignmentStatus { Draft, Published, Superseded, Cancelled }
public enum ShiftSwapStatus { PendingManager, PendingHr, Approved, Rejected, Cancelled }
