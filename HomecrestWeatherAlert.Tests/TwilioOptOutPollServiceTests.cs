using HomecrestWeatherAlert.Data;
using HomecrestWeatherAlert.Models;
using HomecrestWeatherAlert.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace HomecrestWeatherAlert.Tests;

public class TwilioOptOutPollServiceTests
{
    [Fact]
    public async Task EnsureSyncState_reuses_tracked_instance_without_second_Add()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        var first = await service.EnsureSyncStateAsync();
        Assert.Equal(1, first.Id);
        Assert.Equal(EntityState.Added, db.Entry(first).State);

        // Same request / DbContext: must not Add another Id=1 (compose before+after send).
        var second = await service.EnsureSyncStateAsync();
        Assert.Same(first, second);
        Assert.Single(db.OptOutSyncStates.Local);
        Assert.Equal(EntityState.Added, db.Entry(second).State);

        await db.SaveChangesAsync();
        Assert.Equal(EntityState.Unchanged, db.Entry(first).State);

        var third = await service.EnsureSyncStateAsync();
        Assert.Same(first, third);
        Assert.Equal(1, await db.OptOutSyncStates.CountAsync());
    }

    [Fact]
    public async Task EnsureSyncState_loads_existing_row_from_database()
    {
        await using var db = CreateDb();
        var stamped = DateTimeOffset.Parse("2026-10-01T12:00:00Z");
        db.OptOutSyncStates.Add(new OptOutSyncState { Id = 1, LastPolledAt = stamped });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var service = CreateService(db);
        var state = await service.EnsureSyncStateAsync();

        Assert.Equal(1, state.Id);
        Assert.Equal(stamped, state.LastPolledAt);
        Assert.Equal(EntityState.Unchanged, db.Entry(state).State);
        Assert.Equal(1, await db.OptOutSyncStates.CountAsync());
    }

    private static TwilioOptOutPollService CreateService(ApplicationDbContext db)
    {
        return new TwilioOptOutPollService(
            db,
            new EmployeeOptOutService(db, NullLogger<EmployeeOptOutService>.Instance),
            TestIntegrations.CreateSettings(),
            NullLogger<TwilioOptOutPollService>.Instance);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"DataSource=optout-sync-{Guid.NewGuid():N}.db")
            .Options;
        var db = new ApplicationDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }
}

internal static class TestIntegrations
{
    public static IntegrationsSettingsService CreateSettings(IDictionary<string, string?>? config = null)
    {
        var root = Path.Combine(Path.GetTempPath(), "hwa-integrations-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config ?? new Dictionary<string, string?>())
            .Build();

        IHostEnvironment env = new TestHostEnvironment { ContentRootPath = root };

        var protection = DataProtectionProvider.Create(Path.Combine(root, "keys"));
        return new IntegrationsSettingsService(
            configuration,
            protection,
            env,
            NullLogger<IntegrationsSettingsService>.Instance);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "HomecrestWeatherAlert.Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
