using System;
using System.Collections.Generic;
using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public class DigitalWallet : BaseEntity
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal DailyLimit { get; set; }
    public decimal MonthlyLimit { get; set; }
    public decimal CurrentBalance { get; set; } = 0m;
    public string PairingToken { get; set; } = string.Empty;
    public string DeviceStatus { get; set; } = "Disconnected";
    public DateTime? LastSeenAt { get; set; }
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Configurable SMS sender names to listen for. Stored as JSON array, e.g. ["VodafoneCash","VF-Cash"]
    /// </summary>
    public string SmsSenderFilters { get; set; } = "[\"VodafoneCash\"]";

    // Navigation
    public ICollection<RechargeRequest> RechargeRequests { get; set; } = new List<RechargeRequest>();
    public ICollection<IncomingSmsLog> IncomingSmsLogs { get; set; } = new List<IncomingSmsLog>();
}
