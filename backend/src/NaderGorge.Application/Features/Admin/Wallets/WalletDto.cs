using System;
using System.Collections.Generic;

namespace NaderGorge.Application.Features.Admin.Wallets;

public record WalletDto
{
    public Guid Id { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal DailyLimit { get; set; }
    public decimal MonthlyLimit { get; set; }
    public decimal CurrentBalance { get; set; }
    public string PairingToken { get; set; } = string.Empty;
    public string DeviceStatus { get; set; } = string.Empty;
    public DateTime? LastSeenAt { get; set; }
    public bool IsActive { get; set; }
    public List<string> SmsSenderFilters { get; set; } = new();
    
    public decimal DailyReceived { get; set; }
    public decimal MonthlyReceived { get; set; }
    public DateTime CreatedAt { get; set; }
}
