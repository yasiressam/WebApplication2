using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Models;
using WebApplication2.Data;

namespace WebApplication2.Controllers
{
    [Authorize]
    public class NewsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly string _newsUploadPath;

        public NewsController(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _userManager = userManager;
            _webHostEnvironment = webHostEnvironment;

            // ✅ الإبقاء على المسار كما هو
            _newsUploadPath = Path.Combine("C:\\Users", "Public", "MyApp_Uploads", "News");

            if (!Directory.Exists(_newsUploadPath))
            {
                Directory.CreateDirectory(_newsUploadPath);
            }
        }

        // =============== إدارة الأخبار (للسوبر أدمن و NewsEditor) ===============

        [Authorize(Roles = clsRoles.SystemManager + "," + clsRoles.SuperAdmin + "," + clsRoles.NewsEditor)]
        public async Task<IActionResult> Index()
        {
            var news = await _context.News
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            return View(news);
        }

        [Authorize(Roles = clsRoles.SystemManager + "," + clsRoles.SuperAdmin + "," + clsRoles.NewsEditor)]
        public IActionResult Create()
        {
            return View();
        }

        [Authorize(Roles = clsRoles.SystemManager + "," + clsRoles.SuperAdmin + "," + clsRoles.NewsEditor)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(News model, IFormFile? ImageFile)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    string? imageUrl = null;

                    if (ImageFile != null && ImageFile.Length > 0)
                    {
                        if (!IsImageFile(ImageFile))
                        {
                            ModelState.AddModelError("ImageFile", "الملف ليس صورة صالحة");
                            return View(model);
                        }

                        if (!IsValidFileSize(ImageFile))
                        {
                            ModelState.AddModelError("ImageFile", "حجم الصورة أكبر من المسموح (5MB)");
                            return View(model);
                        }

                        imageUrl = await SaveImageAsync(ImageFile);
                    }

                    var currentUser = await _userManager.GetUserAsync(User);

                    var news = new News
                    {
                        Title = model.Title,
                        Content = model.Content,
                        ImageUrl = imageUrl,
                        AuthorId = currentUser?.Id,
                        AuthorName = currentUser?.Email,
                        CreatedAt = DateTime.Now
                    };

                    _context.News.Add(news);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "✅ تم إضافة الخبر بنجاح!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"حدث خطأ: {ex.Message}");
                }
            }

            return View(model);
        }

        [Authorize(Roles = clsRoles.SystemManager + "," + clsRoles.SuperAdmin + "," + clsRoles.NewsEditor)]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var news = await _context.News.FindAsync(id.Value);
            if (news == null)
            {
                return NotFound();
            }

            return View(news);
        }

        // =============== تعديل: Edit (GET) مع إضافة ViewBag ===============
        [Authorize(Roles = clsRoles.SystemManager + "," + clsRoles.SuperAdmin + "," + clsRoles.NewsEditor)]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var news = await _context.News.FindAsync(id.Value);
            if (news == null)
            {
                return NotFound();
            }

            // ✅ إضافة ViewBag للبيانات الإضافية
            ViewBag.NewsId = news.Id;
            ViewBag.CreatedAt = news.CreatedAt;
            ViewBag.AuthorId = news.AuthorId;

            return View(news);
        }

        // =============== تعديل: Edit (POST) مع Bind وتحسين معالجة الأخطاء ===============
        [Authorize(Roles = clsRoles.SystemManager + "," + clsRoles.SuperAdmin + "," + clsRoles.NewsEditor)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Title,Content")] News model, IFormFile? ImageFile, bool? RemoveImage)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var existingNews = await _context.News.FindAsync(id);
                    if (existingNews == null)
                    {
                        return NotFound();
                    }

                    // ✅ تحديث الحقول المسموح بها فقط
                    existingNews.Title = model.Title;
                    existingNews.Content = model.Content;
                    existingNews.UpdatedAt = DateTime.Now;

                    // معالجة الصورة
                    if (RemoveImage == true)
                    {
                        if (!string.IsNullOrEmpty(existingNews.ImageUrl))
                        {
                            DeleteImage(existingNews.ImageUrl);
                            existingNews.ImageUrl = null;
                        }
                    }
                    else if (ImageFile != null && ImageFile.Length > 0)
                    {
                        if (!IsImageFile(ImageFile))
                        {
                            ModelState.AddModelError("ImageFile", "الملف ليس صورة صالحة");
                            // ✅ إعادة تعبئة ViewBag
                            ViewBag.NewsId = existingNews.Id;
                            ViewBag.CreatedAt = existingNews.CreatedAt;
                            ViewBag.AuthorId = existingNews.AuthorId;
                            return View(existingNews);
                        }

                        if (!IsValidFileSize(ImageFile))
                        {
                            ModelState.AddModelError("ImageFile", "حجم الصورة أكبر من المسموح (5MB)");
                            // ✅ إعادة تعبئة ViewBag
                            ViewBag.NewsId = existingNews.Id;
                            ViewBag.CreatedAt = existingNews.CreatedAt;
                            ViewBag.AuthorId = existingNews.AuthorId;
                            return View(existingNews);
                        }

                        if (!string.IsNullOrEmpty(existingNews.ImageUrl))
                        {
                            DeleteImage(existingNews.ImageUrl);
                        }

                        existingNews.ImageUrl = await SaveImageAsync(ImageFile);
                    }

                    _context.News.Update(existingNews);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "✅ تم تحديث الخبر بنجاح!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"حدث خطأ: {ex.Message}");
                }
            }

            // ✅ في حالة فشل التحقق، نعيد الكائن مع تعبئة ViewBag
            var news = await _context.News.FindAsync(id);
            if (news != null)
            {
                ViewBag.NewsId = news.Id;
                ViewBag.CreatedAt = news.CreatedAt;
                ViewBag.AuthorId = news.AuthorId;
                return View(news);
            }

            return View(model);
        }

        [Authorize(Roles = clsRoles.SystemManager + "," + clsRoles.SuperAdmin + "," + clsRoles.NewsEditor)]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var news = await _context.News.FindAsync(id.Value);
            if (news == null)
            {
                return NotFound();
            }

            return View(news);
        }

        [Authorize(Roles = clsRoles.SystemManager + "," + clsRoles.SuperAdmin + "," + clsRoles.NewsEditor)]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var news = await _context.News.FindAsync(id);
                if (news != null)
                {
                    if (!string.IsNullOrEmpty(news.ImageUrl))
                    {
                        DeleteImage(news.ImageUrl);
                    }

                    _context.News.Remove(news);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "✅ تم حذف الخبر بنجاح!";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"❌ حدث خطأ أثناء الحذف: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // =============== عرض الأخبار للجمهور ===============

        [AllowAnonymous]
        public async Task<IActionResult> PublicIndex()
        {
            try
            {
                var news = await _context.News
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(50)
                    .ToListAsync();

                return View(news);
            }
            catch (Exception)
            {
                ViewBag.ErrorMessage = "حدث خطأ في تحميل الأخبار";
                return View(new List<News>());
            }
        }

        [AllowAnonymous]
        public async Task<IActionResult> NewsDetails(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                var news = await _context.News.FindAsync(id.Value);
                if (news == null)
                {
                    TempData["ErrorMessage"] = "❌ الخبر غير موجود أو تم حذفه";
                    return RedirectToAction(nameof(PublicIndex));
                }

                return View(news);
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "❌ حدث خطأ في تحميل الخبر";
                return RedirectToAction(nameof(PublicIndex));
            }
        }

        // =============== API Endpoints ===============

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetNews()
        {
            var news = await _context.News
                .OrderByDescending(n => n.CreatedAt)
                .Take(50)
                .ToListAsync();

            var result = news.Select(n => new
            {
                n.Id,
                n.Title,
                ShortContent = n.Content.Length > 150 ? n.Content.Substring(0, 150) + "..." : n.Content,
                n.ImageUrl,
                n.CreatedAt,
                CreatedAtFormatted = n.CreatedAt.ToString("yyyy-MM-dd HH:mm")
            });

            return Json(new { success = true, data = result });
        }

        [HttpPost]
        [Authorize(Roles = clsRoles.SystemManager + "," + clsRoles.SuperAdmin + "," + clsRoles.NewsEditor)]
        public async Task<IActionResult> UploadImage(IFormFile image)
        {
            if (image == null || image.Length == 0)
            {
                return Json(new { success = false, message = "لم يتم تحديد صورة" });
            }

            if (!IsImageFile(image))
            {
                return Json(new { success = false, message = "الملف ليس صورة" });
            }

            if (!IsValidFileSize(image))
            {
                return Json(new { success = false, message = "حجم الصورة أكبر من المسموح (5MB)" });
            }

            try
            {
                var imageUrl = await SaveImageAsync(image);
                return Json(new { success = true, url = imageUrl });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"حدث خطأ: {ex.Message}" });
            }
        }

        // =============== دوال مساعدة ===============

        private async Task<string> SaveImageAsync(IFormFile imageFile)
        {
            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
            var uploadsFolder = _newsUploadPath;

            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            // ✅ ملاحظة: هذا المسار يجب أن يتوافق مع إعداد Static Files
            // أضف هذا السطر في Program.cs:
            // app.UseStaticFiles(new StaticFileOptions
            // {
            //     FileProvider = new PhysicalFileProvider(@"C:\Users\Public\MyApp_Uploads"),
            //     RequestPath = "/uploads"
            // });
            return "/uploads/news/" + fileName;
        }

        private void DeleteImage(string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl)) return;

            var fileName = Path.GetFileName(imageUrl);
            var filePath = Path.Combine(_newsUploadPath, fileName);

            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }

        private bool IsImageFile(IFormFile file)
        {
            if (file == null) return false;

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            return allowedExtensions.Contains(extension);
        }

        private bool IsValidFileSize(IFormFile file, long maxSizeInBytes = 5 * 1024 * 1024)
        {
            return file.Length <= maxSizeInBytes;
        }
    }
}
