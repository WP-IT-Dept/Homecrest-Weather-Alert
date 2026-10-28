using System.ComponentModel.DataAnnotations;

namespace HomecrestWeatherAlert.Models;

public class Employee
{
    public int Id { get; set; }

    [Required, StringLength(200)]
    [Display(Name = "Name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Original phone input as entered by staff.</summary>
    [Required, StringLength(32)]
    [Display(Name = "Phone")]
    public string Phone { get; set; } = string.Empty;

    /// <summary>Normalized E.164 form used for Twilio sends when available.</summary>
    [StringLength(20)]
    public string? PhoneE164 { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>UTC time the employee texted a STOP/opt-out keyword (null if never opted out).</summary>
    [Display(Name = "Opted out")]
    public DateTimeOffset? OptedOutAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
