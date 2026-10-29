using HomecrestWeatherAlert.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HomecrestWeatherAlert.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<OptOutSyncState> OptOutSyncStates => Set<OptOutSyncState>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Employee>(entity =>
        {
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.Phone);
            entity.HasIndex(e => e.PhoneE164);
        });

        builder.Entity<OptOutSyncState>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedNever();
        });
    }
}
