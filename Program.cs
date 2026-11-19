using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using starfold_mobile.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Shared SQLite database (also used by Paraclete). Default Timeout makes
// writers wait up to 30s on a lock held by the other app instead of failing.
var sqlite = new SqliteConnectionStringBuilder(
    builder.Configuration.GetConnectionString("Starfold")
        ?? throw new InvalidOperationException("ConnectionStrings:Starfold is not set."))
{
    DefaultTimeout = 30
};
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(sqlite.DataSource))!);
builder.Services.AddDbContext<EzraDbContext>(o => o.UseSqlite(
    sqlite.ToString(),
    s => s.MigrationsHistoryTable("ezra_ef_migrations")));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EzraDbContext>();
    db.Database.Migrate();
    db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
