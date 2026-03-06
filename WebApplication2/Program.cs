using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;
using WebApplication2.Services;
using WebApplication2.Hubs;

var builder = WebApplication.CreateBuilder(args);

// ===== Add services =====
// تسجيل الخدمات
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddMemoryCache(); // مهم لـ OTP
builder.Services.AddHttpClient();

// ✅ إضافة Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // مدة الجلسة 30 دقيقة
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// ✅ إضافة SignalR
builder.Services.AddSignalR();

// اتصال بقاعدة البيانات
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// إضافة صفحة الأخطاء في المطور
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// ===== Identity مع Roles =====
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// ===== تسجيل خدمة البريد =====
builder.Services.AddSingleton<IEmailSender, clsEmailConfirm>();

// إضافة Razor Pages المطلوبة لـ Identity
builder.Services.AddRazorPages();

// إضافة Controllers مع Views
builder.Services.AddControllersWithViews();

var app = builder.Build();

// ===== Configure the HTTP request pipeline =====
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// ===== Middleware =====
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession(); // ✅ يجب إضافة UseSession هنا

app.UseAuthentication();
app.UseAuthorization();

// ===== Map routes =====
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    endpoints.MapRazorPages();

    endpoints.MapHub<NotificationHub>("/notificationHub");
});

app.Run();