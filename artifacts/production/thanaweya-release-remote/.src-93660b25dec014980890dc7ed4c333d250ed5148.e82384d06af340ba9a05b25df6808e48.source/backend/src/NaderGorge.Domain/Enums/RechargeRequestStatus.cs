namespace NaderGorge.Domain.Enums;

public enum RechargeRequestStatus
{
    Pending = 0,
    Matched = 1,    // Auto-matched with incoming SMS
    Approved = 2,   // Manually approved by admin
    Rejected = 3,   // Manually rejected by admin
    Expired = 4     // Reservation expired or request timed out
}
