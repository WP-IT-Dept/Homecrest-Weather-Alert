using HomecrestWeatherAlert.Data;
using HomecrestWeatherAlert.Models;
using HomecrestWeatherAlert.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace HomecrestWeatherAlert.Services;

public record OptOutPollResult(
    int InboundMessagesScanned,
    int OptOutsApplied,
    DateTimeOffset PolledAt,
    DateTimeOffset LookbackFrom,
    string? Error);

public class TwilioOptOutPollService
{
    /// <summary>When no prior poll exists, look back this far for inbound STOP messages.</summary>
    public static readonly TimeSpan DefaultLookback = TimeSpan.FromDays(7);

    private readonly ApplicationDbContext _db;
    private readonly EmployeeOptOutService _optOut;
    private readonly TwilioOptions _options;
    private readonly ILogger<TwilioOptOutPollService> _logger;

    public TwilioOptOutPollService(
        ApplicationDbContext db,
        EmployeeOptOutService optOut,
        IOptions<TwilioOptions> options,
        ILogger<TwilioOptOutPollService> logger)
    {
        _db = db;
        _optOut = optOut;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<DateTimeOffset?> GetLastPolledAtAsync(CancellationToken cancellationToken = default)
    {
        var state = await _db.OptOutSyncStates.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return state?.LastPolledAt;
    }

    /// <summary>
    /// Returns the single tracked <see cref="OptOutSyncState"/> row (Id = 1), creating it only when
    /// neither the change tracker nor the database already has one. Safe to call multiple times
    /// on the same <see cref="ApplicationDbContext"/> (e.g. before- and after-send polls).
    /// </summary>
    internal async Task<OptOutSyncState> EnsureSyncStateAsync(CancellationToken cancellationToken = default)
    {
        // FindAsync checks the local tracker first, then the database — avoids a second Add
        // when a prior PollAsync in this request already tracked Id = 1 (saved or not).
        var state = await _db.OptOutSyncStates.FindAsync([1], cancellationToken);
        if (state is not null)
        {
            return state;
        }

        state = new OptOutSyncState { Id = 1 };
        _db.OptOutSyncStates.Add(state);
        return state;
    }

    /// <summary>
    /// Lists Messages sent <em>to</em> our From number since the last poll (inbound replies),
    /// applies STOP keywords, and advances <see cref="OptOutSyncState.LastPolledAt"/>.
    /// </summary>
    public async Task<OptOutPollResult> PollAsync(CancellationToken cancellationToken = default)
    {
        var polledAt = DateTimeOffset.UtcNow;

        if (!_options.IsConfigured)
        {
            return new OptOutPollResult(0, 0, polledAt, polledAt, "Twilio is not configured.");
        }

        var state = await EnsureSyncStateAsync(cancellationToken);

        var lookbackFrom = state.LastPolledAt?.UtcDateTime
            ?? polledAt.UtcDateTime.Subtract(DefaultLookback);

        try
        {
            TwilioSmsSender.InitializeTwilioClient(_options, _logger);

            // Messages addressed TO our Twilio number are inbound replies from employees.
            var messages = await MessageResource.ReadAsync(
                to: new PhoneNumber(_options.FromNumber),
                dateSentAfter: lookbackFrom,
                limit: 1000);

            var scanned = 0;
            var applied = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var message in messages)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (message.Direction != MessageResource.DirectionEnum.Inbound)
                {
                    continue;
                }

                scanned++;

                if (!SmsOptOutKeywords.IsOptOut(message.Body))
                {
                    continue;
                }

                var from = message.From?.ToString();
                if (string.IsNullOrWhiteSpace(from) || !seen.Add(from))
                {
                    continue;
                }

                var id = await _optOut.ApplyOptOutAsync(from, cancellationToken);
                if (id.HasValue)
                {
                    applied++;
                }
            }

            state.LastPolledAt = polledAt;
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Opt-out poll complete: scanned {Scanned} inbound, applied {Applied}, lookback from {From:o}.",
                scanned,
                applied,
                lookbackFrom);

            return new OptOutPollResult(
                scanned,
                applied,
                polledAt,
                new DateTimeOffset(DateTime.SpecifyKind(lookbackFrom, DateTimeKind.Utc)),
                null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Opt-out poll failed.");
            return new OptOutPollResult(
                0,
                0,
                polledAt,
                new DateTimeOffset(DateTime.SpecifyKind(lookbackFrom, DateTimeKind.Utc)),
                ex.Message);
        }
    }
}
