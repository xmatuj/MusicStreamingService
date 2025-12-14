using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using MusicStreamingService.Controllers;
using MusicStreamingService.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Настройка аутентификации
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    });

// Настройка авторизации
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdmin", policy =>
        policy.RequireRole("Admin"));

    options.AddPolicy("RequireMusician", policy =>
        policy.RequireRole("Musician", "Admin"));

    options.AddPolicy("RequireSubscriber", policy =>
        policy.RequireRole("Subscriber", "Admin"));
});

// MySQL Configuration
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new MySqlServerVersion(new Version(9, 5, 0))
    ));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Create database if not exists (for development)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.EnsureCreated();
}

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "tracks",
    pattern: "Tracks/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "search",
    pattern: "Search/{action=Index}/{id?}",
    defaults: new { controller = "Search" });

app.MapControllerRoute(
    name: "statistics",
    pattern: "Statistics/{action}/{id?}",
    defaults: new { controller = "Statistics" });

app.Use(async (context, next) =>
{
    Console.WriteLine($"=== Request: {context.Request.Path}");
    Console.WriteLine($"User authenticated: {context.User.Identity?.IsAuthenticated}");
    Console.WriteLine($"User name: {context.User.Identity?.Name}");

    if (context.User.Identity?.IsAuthenticated == true)
    {
        Console.WriteLine($"Claims:");
        foreach (var claim in context.User.Claims)
        {
            Console.WriteLine($"  {claim.Type}: {claim.Value}");
        }
    }

    await next();
});

// Фоновая задача для проверки подписок
var subscriptionTimer = new System.Timers.Timer(TimeSpan.FromHours(1).TotalMilliseconds);
subscriptionTimer.Elapsed += async (sender, e) =>
{
    using var scope = app.Services.CreateScope();
    await SubscriptionController.CheckExpiredSubscriptions(scope.ServiceProvider);
};
subscriptionTimer.Start();

// Убираем подписку при остановке приложения
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() => subscriptionTimer.Stop());

app.Run();