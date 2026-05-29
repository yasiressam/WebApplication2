using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;
using WebApplication2.Services;
using WebApplication2.Hubs;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// ===== Add services =====
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.Configure<WhatsAppApiSettings>(builder.Configuration.GetSection("WhatsAppApi"));
builder.Services.AddScoped<IWhatsAppService, WhatsAppService>();
builder.Services.AddHostedService<RequestCleanupService>();
builder.Services.AddHostedService<NotificationCleanupService>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();

// ❌ إزالة تسجيل الـ Repositories و UnitOfWork
// builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
// builder.Services.AddScoped<IIdentifyRepository, IdentifyRepository>();
// builder.Services.AddScoped<INewsRepository, NewsRepository>();
// builder.Services.AddScoped<IRequestRepository, RequestRepository>();
// builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
// builder.Services.AddScoped<IMembershipRepository, MembershipRepository>();
// builder.Services.AddScoped<IUserDeviceRepository, UserDeviceRepository>();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddSignalR();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;

    // ✅ كلمة مرور قوية
    options.Password.RequireDigit = true;              // رقم
    options.Password.RequiredLength = 6;              // 8 أحرف
    options.Password.RequireNonAlphanumeric = true;   // رمز
    options.Password.RequireUppercase = true;         // حرف كبير
    options.Password.RequireLowercase = true;         // حرف صغير
    options.Password.RequiredUniqueChars = 1;

    // حماية تسجيل الدخول
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender, clsEmailConfirm>();
builder.Services.AddRazorPages();
builder.Services.AddControllersWithViews();

var app = builder.Build();

try
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var normalizedCount = await BaghdadWorkLocationNormalizer.NormalizeAsync(dbContext);
    if (normalizedCount > 0)
    {
        Console.WriteLine($"✅ تم تحويل بيانات بغداد القديمة تلقائياً: {normalizedCount} سجل");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️ تعذر تحويل بيانات بغداد القديمة تلقائياً: {ex.Message}");
}

// ============================================================
// ✅ ✅ ✅ إنشاء المجلدات تلقائياً ✅ ✅ ✅
// ============================================================
try
{
    var baseUploadPath = Path.Combine("C:\\Users", "Public", "MyApp_Uploads");

    var newsPath = Path.Combine(baseUploadPath, "News");
    var profilesPath = Path.Combine(baseUploadPath, "Profiles");
    var requestsPath = Path.Combine(baseUploadPath, "Requests");

    if (!Directory.Exists(baseUploadPath)) Directory.CreateDirectory(baseUploadPath);
    if (!Directory.Exists(newsPath)) Directory.CreateDirectory(newsPath);
    if (!Directory.Exists(profilesPath)) Directory.CreateDirectory(profilesPath);
    if (!Directory.Exists(requestsPath)) Directory.CreateDirectory(requestsPath);

    Console.WriteLine($"✅ تم إنشاء مجلد الرفع: {baseUploadPath}");
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️ خطأ في إنشاء المجلدات: {ex.Message}");
}
// ============================================================

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

app.UseHttpsRedirection();
app.UseStaticFiles();

// ✅ ✅ ✅ إضافة خدمة الملفات الثابتة للمجلدات الخارجية ✅ ✅ ✅
try
{
    var baseUploadPath = Path.Combine("C:\\Users", "Public", "MyApp_Uploads");
    var newsPath = Path.Combine(baseUploadPath, "News");
    var profilesPath = Path.Combine(baseUploadPath, "Profiles");
    var requestsPath = Path.Combine(baseUploadPath, "Requests");

    if (Directory.Exists(newsPath))
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(newsPath),
            RequestPath = "/uploads/news"
        });
    }

    if (Directory.Exists(profilesPath))
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(profilesPath),
            RequestPath = "/MyApp_Uploads/Profiles"
        });
    }

    if (Directory.Exists(requestsPath))
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(requestsPath),
            RequestPath = "/MyApp_Uploads/Requests"
        });
    }
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️ خطأ في إضافة خدمة الملفات الثابتة: {ex.Message}");
}

// ============================================================
// ✅ ✅ ✅ إصلاح روابط التأكيد ✅ ✅ ✅
// ============================================================
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.Use(async (context, next) =>
{
    var request = context.Request;
    var domain = "assaib.com";
    var port = request.IsHttps ? 443 : 80;

    if (request.Host.Host != domain && !request.Host.Host.Contains("localhost"))
    {
        request.Host = new HostString(domain, port);
    }

    await next();
});
// ============================================================

app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    endpoints.MapRazorPages();
    endpoints.MapHub<NotificationHub>("/notificationHub");
});


app.Run();
