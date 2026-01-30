using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models; // يحتوي clsEmailConfirm
using Microsoft.AspNetCore.Identity.UI.Services;

var builder = WebApplication.CreateBuilder(args);

// ===== Add services =====

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
    options.SignIn.RequireConfirmedAccount = true; // البريد مطلوب للتأكيد
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

// ⚠️ Authentication يجب أن يكون قبل Authorization
app.UseAuthentication();
app.UseAuthorization();

// ===== Map routes =====
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Razor Pages (مطلوبة لـ Identity)
app.MapRazorPages();

app.Run();
