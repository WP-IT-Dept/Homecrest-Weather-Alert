using HomecrestWeatherAlert.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace HomecrestWeatherAlert.Tests;

public class SeedAdminProtectionTests
{
    [Fact]
    public void Default_seed_admin_email_is_protected()
    {
        var user = new IdentityUser
        {
            Email = SeedData.DefaultAdminEmail,
            UserName = SeedData.DefaultAdminEmail
        };

        Assert.True(SeedData.IsProtectedSeedAdmin(user));
    }

    [Fact]
    public void Configured_Seed_AdminEmail_is_protected()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:AdminEmail"] = "bootstrap@homecrest.local"
            })
            .Build();

        var user = new IdentityUser
        {
            Email = "bootstrap@homecrest.local",
            UserName = "bootstrap@homecrest.local"
        };

        Assert.True(SeedData.IsProtectedSeedAdmin(user, config));
    }

    [Fact]
    public void Ordinary_users_are_not_protected()
    {
        var user = new IdentityUser
        {
            Email = "staff@homecrest.local",
            UserName = "staff@homecrest.local"
        };

        Assert.False(SeedData.IsProtectedSeedAdmin(user));
    }
}
