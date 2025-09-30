using IT15.Data;
using IT15.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Resend;
using Npgsql;


var builder = WebApplication.CreateBuilder(args);



// Get DATABASE_URL from Render env vars
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
if (string.IsNullOrEmpty(databaseUrl))
{
    throw new InvalidOperationException("DATABASE_URL environment variable not set.");
}

// Parse the DATABASE_URL into a proper connection string for Npgsql
var databaseUri = new Uri(databaseUrl);
var userInfo = databaseUri.UserInfo.Split(':');

var port = databaseUri.Port != -1 ? databaseUri.Port : 5432;

var connectionString = new NpgsqlConnectionStringBuilder
{
    Host = databaseUri.Host,
    Port = databaseUri.Port,
    Username = userInfo[0],
    Password = userInfo[1],
    Database = databaseUri.AbsolutePath.TrimStart('/'),
    SslMode = SslMode.Require,
    TrustServerCertificate = true
}.ToString();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));


builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddScoped<IT15.Services.PayrollService>();

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Configuration
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables();

builder.Logging.AddConsole();

// --- KEY CHANGES ---

// 1. Register your custom EmailSender service
builder.Services.AddTransient<IEmailSender, EmailSender>();
builder.Services.AddTransient<ISmsSender, SmsSender>();
builder.Services.AddHttpClient<HolidayApiService>();

builder.Services.AddHttpClient<IncomeApiService>(client =>
{
    client.BaseAddress = new Uri("https://fakestoreapi.com/");
});

builder.Services.AddScoped<IAuditService, AuditService>();

// Configure Resend using the recommended IHttpClientFactory approach.


builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/Admin"))
        {
            context.Response.Redirect("/Admin/Account/Login");
        }
        else if (context.Request.Path.StartsWithSegments("/HumanResource"))
        {
            context.Response.Redirect("/HumanResource/Account/Login");
        }
        else if (context.Request.Path.StartsWithSegments("/Accounting"))
        {
            context.Response.Redirect("/Accounting/Account/Login");
        }
        else
        {
            context.Response.Redirect("/Identity/Account/Login");
        }
        return Task.CompletedTask;
    };
});

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Seed the database
// Seed the database and apply migrations
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        db.Database.Migrate(); //  applies pending migrations automatically

        var configuration = services.GetRequiredService<IConfiguration>();
        await SeedData.Initialize(services, configuration);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}


// Configure the HTTP request pipeline.
// THIS IS THE MOST IMPORTANT SECTION FOR DEBUGGING
if (app.Environment.IsDevelopment())
{
    // This will replace the "page not exist" error with a detailed error report.
    app.UseDeveloperExceptionPage();
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// The order of these three lines is CRITICAL for routing and security to work.
app.UseRouting();
app.UseAuthentication(); // <-- This was likely missing and is required.
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();

