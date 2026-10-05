namespace NaderGorge.Domain.Entities;

public sealed class ExamWhatsAppDeliveryEvent
{
    public string Fingerprint { get; set; } = "";
    public string BusinessAccountId { get; set; } = "";
    public string PhoneNumberId { get; set; } = "";
    public string MessageId { get; set; } = "";
    public string Status { get; set; } = "";
    public long EventUnixTime { get; set; }
    public int? ErrorCode { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}
