using System.ComponentModel.DataAnnotations;
using HomecrestWeatherAlert.Data;
using HomecrestWeatherAlert.Models;
using HomecrestWeatherAlert.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HomecrestWeatherAlert.Pages.Compose;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly ISmsSender _smsSender;
    private readonly TwilioOptOutPollService _optOutPoll;

    public IndexModel(ApplicationDbContext db, ISmsSender smsSender, TwilioOptOutPollService optOutPoll)
    {
        _db = db;
        _smsSender = smsSender;
        _optOutPoll = optOutPoll;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public int ActiveRecipientCount { get; set; }
    public int SkippedInvalidPhoneCount { get; set; }
    public bool TwilioConfigured { get; set; }
    public DateTimeOffset? LastOptOutPollAt { get; set; }
    public OptOutPollResult? LastPollResult { get; set; }
    public IReadOnlyList<SmsSendResult>? Results { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Enter a message to send.")]
        [StringLength(1600, ErrorMessage = "Message must be 1600 characters or fewer.")]
        [Display(Name = "Message")]
        public string Message { get; set; } = string.Empty;

        [Display(Name = "I confirm this will send individual SMS to all active employees")]
        public bool ConfirmSend { get; set; }
    }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        // Sync STOP opt-outs from Twilio before showing recipient counts.
        if (_smsSender.IsConfigured)
        {
            LastPollResult = await _optOutPoll.PollAsync(cancellationToken);
            if (LastPollResult.Error is null && LastPollResult.OptOutsApplied > 0)
            {
                TempData["StatusMessage"] =
                    $"Synced opt-outs: marked {LastPollResult.OptOutsApplied} employee(s) inactive.";
            }
        }

        await LoadCountsAsync();
    }

    public async Task<IActionResult> OnPostSyncOptOutsAsync(CancellationToken cancellationToken)
    {
        LastPollResult = await _optOutPoll.PollAsync(cancellationToken);
        if (LastPollResult.Error is not null)
        {
            TempData["ErrorMessage"] = $"Opt-out sync failed: {LastPollResult.Error}";
        }
        else
        {
            TempData["StatusMessage"] =
                $"Opt-out sync complete. Inbound scanned: {LastPollResult.InboundMessagesScanned}; newly opted out: {LastPollResult.OptOutsApplied}.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        // Refresh opt-outs immediately before sending so STOPs since last visit are honored.
        if (_smsSender.IsConfigured)
        {
            LastPollResult = await _optOutPoll.PollAsync(cancellationToken);
        }

        await LoadCountsAsync();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (!Input.ConfirmSend)
        {
            ModelState.AddModelError(nameof(Input.ConfirmSend), "Confirm before sending.");
            return Page();
        }

        if (!TwilioConfigured)
        {
            ModelState.AddModelError(string.Empty, "Twilio is not configured. Set AccountSid, ApiKey, ApiSecret, and FromNumber before sending.");
            return Page();
        }

        var employees = await _db.Employees
            .AsNoTracking()
            .Where(e => e.IsActive)
            .OrderBy(e => e.Name)
            .ToListAsync(cancellationToken);

        var recipients = new List<(int EmployeeId, string Name, string PhoneE164)>();
        foreach (var employee in employees)
        {
            var e164 = employee.PhoneE164 ?? PhoneNormalizer.ToE164(employee.Phone);
            if (e164 is null)
            {
                SkippedInvalidPhoneCount++;
                continue;
            }

            recipients.Add((employee.Id, employee.Name, e164));
        }

        ActiveRecipientCount = recipients.Count;

        if (recipients.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "No active employees with valid phone numbers to message.");
            return Page();
        }

        Results = await _smsSender.SendIndividualAsync(Input.Message.Trim(), recipients, cancellationToken);

        // Poll again after send in case replies arrive quickly (also advances the watermark).
        if (_smsSender.IsConfigured)
        {
            await _optOutPoll.PollAsync(cancellationToken);
            LastOptOutPollAt = await _optOutPoll.GetLastPolledAtAsync(cancellationToken);
        }

        return Page();
    }

    private async Task LoadCountsAsync()
    {
        TwilioConfigured = _smsSender.IsConfigured;
        LastOptOutPollAt = await _optOutPoll.GetLastPolledAtAsync();

        var active = await _db.Employees
            .AsNoTracking()
            .Where(e => e.IsActive)
            .Select(e => new { e.Phone, e.PhoneE164 })
            .ToListAsync();

        ActiveRecipientCount = active.Count(e => (e.PhoneE164 ?? PhoneNormalizer.ToE164(e.Phone)) is not null);
        SkippedInvalidPhoneCount = active.Count - ActiveRecipientCount;
    }
}
