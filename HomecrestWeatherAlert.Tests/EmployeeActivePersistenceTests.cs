using System.Net;
using System.Text.RegularExpressions;
using HomecrestWeatherAlert.Data;
using HomecrestWeatherAlert.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HomecrestWeatherAlert.Tests;

public class EmployeeActivePersistenceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _dbName = $"test-isactive-{Guid.NewGuid():N}.db";

    public EmployeeActivePersistenceTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection", $"DataSource={_dbName};Cache=Shared");
            builder.UseSetting("Seed:AdminPassword", "ChangeMeAdmin1!");
        });
    }

    [Fact]
    public async Task Edit_save_with_Active_select_persists_and_reloads()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        int employeeId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.EnsureDeletedAsync();
            await SeedData.InitializeAsync(scope.ServiceProvider);

            db.Employees.Add(new Employee
            {
                Name = "Inactive Person",
                Phone = "555-100-2000",
                PhoneE164 = "+15551002000",
                IsActive = false
            });
            await db.SaveChangesAsync();
            employeeId = await db.Employees.Select(e => e.Id).SingleAsync();
            Assert.False(await db.Employees.Where(e => e.Id == employeeId).Select(e => e.IsActive).SingleAsync());
        }

        await LoginAsync(client);

        var editGet = await client.GetAsync($"/Employees/Edit/{employeeId}");
        editGet.EnsureSuccessStatusCode();
        var editHtml = await editGet.Content.ReadAsStringAsync();
        Assert.Matches("(?is)<option[^>]*selected[^>]*value=\"False\"|value=\"False\"[^>]*selected", editHtml);
        var token = Antiforgery(editHtml);

        var post = await client.PostAsync($"/Employees/Edit/{employeeId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Input.Id"] = employeeId.ToString(),
            ["Input.Name"] = "Inactive Person",
            ["Input.Phone"] = "555-100-2000",
            ["Input.IsActive"] = "True"
        }));

        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        Assert.Equal("/Employees", post.Headers.Location?.OriginalString);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var employee = await db.Employees.AsNoTracking().SingleAsync(e => e.Id == employeeId);
            Assert.True(employee.IsActive);
        }

        var reload = await client.GetAsync($"/Employees/Edit/{employeeId}");
        reload.EnsureSuccessStatusCode();
        var reloadHtml = await reload.Content.ReadAsStringAsync();
        Assert.Matches("(?is)<option[^>]*selected[^>]*value=\"True\"|value=\"True\"[^>]*selected", reloadHtml);
        Assert.DoesNotMatch("(?is)<option[^>]*selected[^>]*value=\"False\"|value=\"False\"[^>]*selected", reloadHtml);

        var list = await client.GetAsync("/Employees");
        list.EnsureSuccessStatusCode();
        var listHtml = await list.Content.ReadAsStringAsync();
        Assert.Contains("Inactive Person", listHtml);
        Assert.Matches(new Regex("Inactive Person[\\s\\S]*?>Yes<", RegexOptions.IgnoreCase), listHtml);
    }

    [Fact]
    public async Task Create_defaults_to_Active_and_persists()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.EnsureDeletedAsync();
            await SeedData.InitializeAsync(scope.ServiceProvider);
        }

        await LoginAsync(client);

        var createGet = await client.GetAsync("/Employees/Create");
        createGet.EnsureSuccessStatusCode();
        var html = await createGet.Content.ReadAsStringAsync();
        Assert.Matches("(?is)<option[^>]*selected[^>]*value=\"True\"|value=\"True\"[^>]*selected", html);
        var token = Antiforgery(html);

        var post = await client.PostAsync("/Employees/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Input.Name"] = "New Hire",
            ["Input.Phone"] = "555-300-4000",
            ["Input.IsActive"] = "True"
        }));
        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var employee = await db.Employees.AsNoTracking().SingleAsync(e => e.Name == "New Hire");
            Assert.True(employee.IsActive);
        }
    }

    private static async Task LoginAsync(HttpClient client)
    {
        var loginGet = await client.GetAsync("/Identity/Account/Login");
        loginGet.EnsureSuccessStatusCode();
        var html = await loginGet.Content.ReadAsStringAsync();
        var token = Antiforgery(html);

        var loginPost = await client.PostAsync("/Identity/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Input.Email"] = SeedData.DefaultAdminEmail,
            ["Input.Password"] = "ChangeMeAdmin1!",
            ["Input.RememberMe"] = "false"
        }));
        Assert.True(
            loginPost.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found or HttpStatusCode.SeeOther,
            $"Login failed: {(int)loginPost.StatusCode}");
    }

    private static string Antiforgery(string html)
    {
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, "Antiforgery token not found");
        return match.Groups[1].Value;
    }
}
