using HomecrestWeatherAlert.Pages.Employees;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace HomecrestWeatherAlert.Tests;

public class EmployeeIsActiveTests
{
    [Theory]
    [InlineData(new[] { "true" }, true)]
    [InlineData(new[] { "false" }, false)]
    [InlineData(new[] { "True" }, true)]
    [InlineData(new[] { "False" }, false)]
    [InlineData(new[] { "true", "false" }, true)]  // checkbox then hidden
    [InlineData(new[] { "false", "true" }, true)]  // hidden then checkbox (old bug order)
    [InlineData(new[] { "false", "false" }, false)]
    public void ReadIsActive_resolves_select_and_checkbox_posts(string[] posted, bool expected)
    {
        var form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["Input.IsActive"] = new StringValues(posted)
        });

        var result = CreateModel.ReadIsActive(form, modelBoundFallback: !expected);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ReadIsActive_falls_back_when_field_missing()
    {
        var form = new FormCollection(new Dictionary<string, StringValues>());
        Assert.True(CreateModel.ReadIsActive(form, modelBoundFallback: true));
        Assert.False(CreateModel.ReadIsActive(form, modelBoundFallback: false));
    }
}
