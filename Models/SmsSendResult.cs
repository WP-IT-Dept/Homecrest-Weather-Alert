namespace HomecrestWeatherAlert.Models;

public class SmsSendResult
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string PhoneMasked { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? TwilioSid { get; set; }
    public string? Error { get; set; }
}
