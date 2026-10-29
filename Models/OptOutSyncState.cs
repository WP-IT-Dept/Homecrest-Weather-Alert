namespace HomecrestWeatherAlert.Models;

/// <summary>Single-row table tracking the last Twilio inbound opt-out poll.</summary>
public class OptOutSyncState
{
    public int Id { get; set; } = 1;

    /// <summary>UTC time of the last successful Messages list poll.</summary>
    public DateTimeOffset? LastPolledAt { get; set; }
}
