using Microsoft.AspNetCore.Mvc.Rendering;

namespace HomecrestWeatherAlert.Pages.Employees;

public static class EmployeeActiveOptions
{
    // Use Boolean.ToString() values so asp-for selection matching works.
    public static List<SelectListItem> Items { get; } =
    [
        new SelectListItem("Active", true.ToString()),
        new SelectListItem("Inactive", false.ToString())
    ];
}
