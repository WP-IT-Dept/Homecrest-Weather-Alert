using HomecrestWeatherAlert.Models;

namespace HomecrestWeatherAlert.Services;

public interface ISmsSender
{
    /// <summary>
    /// Send one Twilio MessageResource.Create per recipient. Never batches numbers.
    /// </summary>
    Task<IReadOnlyList<SmsSendResult>> SendIndividualAsync(
        string body,
        IEnumerable<(int EmployeeId, string Name, string PhoneE164)> recipients,
        CancellationToken cancellationToken = default);

    bool IsConfigured { get; }
}
