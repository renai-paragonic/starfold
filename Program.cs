using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using starfold_mobile.Data;

// `dotnet run -- hash-password` prints a hash for Ezra__AdminPasswordHash.
if (args is ["hash-password", ..])
{
    var password = ReadPassword("Password: ");
    if (password.Length == 0 || password != ReadPassword("Again: "))
    {
        Console.Error.WriteLine("Passwords were empty or didn't match.");
        return 1;
    }
    Console.WriteLine(new PasswordHasher<object>().HashPassword(null!, password));
    return 0;
}

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))
    .AddRazorOptions(o => o.ViewLocationFormats.Add("/Views/Ezra/{1}/{0}.cshtml"));

// Shared SQLite database (also used by Paraclete). Default Timeout makes
// writers wait up to 30s on a lock held by the other app instead of failing.
var sqlite = new SqliteConnectionStringBuilder(
    builder.Configuration.GetConnectionString("Starfold")
        ?? throw new InvalidOperationException("ConnectionStrings:Starfold is not set."))
{
    DefaultTimeout = 30
};
var dataDir = Path.GetDirectoryName(Path.GetFullPath(sqlite.DataSource))!;
Directory.CreateDirectory(dataDir);
builder.Services.AddDbContext<EzraDbContext>(o => o.UseSqlite(
    sqlite.ToString(),
    // Split queries: notes load both tags and refs, and one joined query
    // would multiply the rows.
    s => s.MigrationsHistoryTable("ezra_ef_migrations").UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));

// Ezra sign-in: one admin password (hash in Ezra:AdminPasswordHash), kept in a cookie.
// Keys live next to the database so logins survive restarts and redeploys.
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDir, "keys")))
    .SetApplicationName("starfold");
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/ezra/login";
        o.LogoutPath = "/ezra/logout";
        o.Cookie.Name = "starfold.auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.ExpireTimeSpan = TimeSpan.FromDays(30);
        o.SlidingExpiration = true;
    });
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(5) }));
});

// Behind nginx: take the client IP and scheme from X-Forwarded-*. Only
// loopback proxies are trusted by default.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EzraDbContext>();
    db.Database.Migrate();
    db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
}

app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
return 0;

// Reads a line without echoing it (falls back to a plain read when piped).
static string ReadPassword(string prompt)
{
    Console.Write(prompt);
    if (Console.IsInputRedirected) return Console.ReadLine() ?? "";
    var chars = new List<char>();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter) break;
        if (key.Key == ConsoleKey.Backspace) { if (chars.Count > 0) chars.RemoveAt(chars.Count - 1); }
        else if (!char.IsControl(key.KeyChar)) chars.Add(key.KeyChar);
    }
    Console.WriteLine();
    return new string([.. chars]);
}
