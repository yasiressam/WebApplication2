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
var httpsPort = GetHttpsPort(builder.Configuration);

if (httpsPort.HasValue)
{
    builder.Services.AddHttpsRedirection(options =>
    {
        options.HttpsPort = httpsPort.Value;
        options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
    });
}

// ===== Add services =====
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IAuditTrailService, DbAuditTrailService>();
builder.Services.AddScoped<AuditActivityFilter>();
builder.Services.Configure<OtpApiSettings>(builder.Configuration.GetSection("OtpApi"));
builder.Services.AddHostedService<RequestCleanupService>();
builder.Services.AddHostedService<NotificationCleanupService>();
builder.Services.AddHostedService<AuditLogCleanupService>();
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
    options.IdleTimeout = TimeSpan.FromMinutes(15);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddSignalR();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null);

        sqlOptions.CommandTimeout(60);
    });

    if (builder.Environment.IsDevelopment())
    {
        options.EnableDetailedErrors();
    }
});

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

builder.Services.ConfigureApplicationCookie(options =>
{
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

builder.Services.AddSingleton<IEmailSender, clsEmailConfirm>();
builder.Services.AddRazorPages();
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.AddService<AuditActivityFilter>();
});

var app = builder.Build();

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

app.UseMiddleware<AuditErrorMiddleware>();
if (httpsPort.HasValue)
{
    app.UseHttpsRedirection();
}
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


static int? GetHttpsPort(ConfigurationManager configuration)
{
    var configuredPort = configuration.GetValue<int?>("HTTPS_PORT")
        ?? configuration.GetValue<int?>("ASPNETCORE_HTTPS_PORT");

    if (configuredPort.HasValue)
    {
        return configuredPort.Value;
    }

    var urls = configuration["ASPNETCORE_URLS"] ?? configuration["urls"];
    if (string.IsNullOrWhiteSpace(urls))
    {
        return null;
    }

    foreach (var url in urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return uri.Port;
        }
    }

    return null;
}

app.Run();
