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

    public async Task OnGetAsync()
    {
        await LoadCountsAsync();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        // Poll STOP opt-outs immediately before send so newly opted-out people are skipped.
        if (_smsSender.IsConfigured)
        {
            await _optOutPoll.PollAsync(cancellationToken);
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

        // Optional after-send poll advances the watermark if any quick replies arrived.
        if (_smsSender.IsConfigured)
        {
            await _optOutPoll.PollAsync(cancellationToken);
        }

        return Page();
    }

    private async Task LoadCountsAsync()
    {
        TwilioConfigured = _smsSender.IsConfigured;

        var active = await _db.Employees
            .AsNoTracking()
            .Where(e => e.IsActive)
            .Select(e => new { e.Phone, e.PhoneE164 })
            .ToListAsync();

        ActiveRecipientCount = active.Count(e => (e.PhoneE164 ?? PhoneNormalizer.ToE164(e.Phone)) is not null);
        SkippedInvalidPhoneCount = active.Count - ActiveRecipientCount;
    }
}
