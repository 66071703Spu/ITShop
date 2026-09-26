using ITShop.Models;
using ITShop.Helpers;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSession();
builder.Services.AddSingleton<PasswordResetEmailSender>();
builder.Services.AddDbContext<Csi402dbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("DefaultConnection"))
    ));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<Csi402dbContext>();
    DatabaseSchemaInitializer.EnsureBrandSchema(db);

    if (args.Contains("--rotate-test-admin-passwords", StringComparer.OrdinalIgnoreCase))
    {
        foreach (var message in TestAdminPasswordRotator.Rotate(db, builder.Configuration))
        {
            Console.WriteLine(message);
        }

        return;
    }

    if (args.Contains("--seed-test-accounts", StringComparer.OrdinalIgnoreCase))
    {
        if (!app.Environment.IsDevelopment())
        {
            throw new InvalidOperationException("Test accounts can only be seeded in Development.");
        }

        var testPassword = builder.Configuration["TestAccounts:Password"];
        if (string.IsNullOrWhiteSpace(testPassword))
        {
            throw new InvalidOperationException("Set TestAccounts__Password before seeding test accounts.");
        }

        foreach (var message in TestAccountSeeder.Seed(db, testPassword))
        {
            Console.WriteLine(message);
        }

        return;
    }

    if (args.Contains("--migrate-passwords", StringComparer.OrdinalIgnoreCase))
    {
        Console.WriteLine($"Migrated {UserPasswordService.MigrateLegacyPasswords(db)} legacy passwords.");
        return;
    }

    if (args.Contains("--repair-comset-seed", StringComparer.OrdinalIgnoreCase))
    {
        Console.WriteLine($"Removed {DatabaseSchemaInitializer.RemoveSampleComsetPlaceholder(db)} placeholder package items.");
        return;
    }

    if (args.Contains("--repair-comset-image", StringComparer.OrdinalIgnoreCase))
    {
        Console.WriteLine($"Updated {DatabaseSchemaInitializer.ReplaceSampleComsetImage(db)} sample Comset image.");
        return;
    }

    if (args.Contains("--repair-pending-tracking", StringComparer.OrdinalIgnoreCase))
    {
        Console.WriteLine($"Cleared {DatabaseSchemaInitializer.ClearGeneratedPendingTrackingNumbers(db)} generated tracking numbers.");
        return;
    }

    if (args.Contains("--repair-catalog-images", StringComparer.OrdinalIgnoreCase))
    {
        Console.WriteLine($"Updated {DatabaseSchemaInitializer.ReplaceBulkProductImages(db)} product images.");
        return;
    }
}


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();



