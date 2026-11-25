using FiveAndOne.Application.Services.Users;

namespace FiveAndOne.API.Seeding;

public static class SuperAdminSeeder
{
    /// <summary>
    /// Creates the first SuperAdmin from the "SuperAdmin" config section if none exists yet.
    /// Does nothing when the section is missing, or when a SuperAdmin already exists.
    /// </summary>
    public static async Task SeedSuperAdminAsync(this WebApplication app)
    {
        var section = app.Configuration.GetSection("SuperAdmin");
        var email = section["Email"];
        var password = section["Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        using var scope = app.Services.CreateScope();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();

        await userService.EnsureSuperAdminAsync(
            section["FirstName"] ?? "Super",
            section["Username"] ?? "superadmin",
            email,
            password);
    }
}
