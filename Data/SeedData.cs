using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HomecrestWeatherAlert.Data;

public static class SeedData
{
    public const string AdminRole = "Admin";
    public const string UserRole = "User";

    public const string DefaultAdminEmail = "admin@homecrest.local";

    /// <summary>
    /// Default password used only when HOMECREST_ADMIN_PASSWORD is not set.
    /// Change immediately after first login in production.
    /// </summary>
    public const string DefaultAdminPasswordFallback = "ChangeMe!Admin1";

    /// <summary>
    /// True for the seeded bootstrap admin (default or Seed:AdminEmail).
    /// That account cannot be disabled, demoted, or deleted from Admin → Users.
    /// </summary>
    public static bool IsProtectedSeedAdmin(IdentityUser user, IConfiguration? configuration = null)
    {
        var email = user.Email ?? user.UserName;
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        if (string.Equals(email, DefaultAdminEmail, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var configured = configuration?["Seed:AdminEmail"];
        return !string.IsNullOrWhiteSpace(configured)
            && string.Equals(email, configured, StringComparison.OrdinalIgnoreCase);
    }

    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var db = sp.GetRequiredService<ApplicationDbContext>();
        // Migrate schema only — never bulk-update Employees or reset IsActive on startup.
        await db.Database.MigrateAsync();

        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { AdminRole, UserRole })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var userManager = sp.GetRequiredService<UserManager<IdentityUser>>();
        var config = sp.GetRequiredService<IConfiguration>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("SeedData");

        var adminEmail = config["Seed:AdminEmail"] ?? DefaultAdminEmail;
        var adminPassword = config["Seed:AdminPassword"]
            ?? Environment.GetEnvironmentVariable("HOMECREST_ADMIN_PASSWORD")
            ?? DefaultAdminPasswordFallback;

        var admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin is null)
        {
            admin = new IdentityUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                LockoutEnabled = true
            };

            var create = await userManager.CreateAsync(admin, adminPassword);
            if (!create.Succeeded)
            {
                throw new InvalidOperationException(
                    "Failed to seed default admin: " + string.Join("; ", create.Errors.Select(e => e.Description)));
            }

            logger.LogInformation("Seeded default admin user {Email}", adminEmail);
        }

        if (!await userManager.IsInRoleAsync(admin, AdminRole))
        {
            await userManager.AddToRoleAsync(admin, AdminRole);
        }

        if (!await userManager.IsInRoleAsync(admin, UserRole))
        {
            await userManager.AddToRoleAsync(admin, UserRole);
        }
    }
}
