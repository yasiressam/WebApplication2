using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;
using System.IO;

namespace WebApplication2.Controllers
{
    public class NewsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public NewsController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // =============== إدارة الأخبار (للسوبر أدمن فقط) ===============

        [Authorize(Roles = clsRoles.SuperAdmin)]
        public async Task<IActionResult> Index()
        {
            var newsList = await _context.News
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
            return View(newsList);
        }

        [Authorize(Roles = clsRoles.SuperAdmin)]
        public IActionResult Create()
        {
            return View();
        }

        [Authorize(Roles = clsRoles.SuperAdmin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(News news, IFormFile? ImageFile)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // حفظ الصورة إذا تم رفعها
                    if (ImageFile != null && ImageFile.Length > 0)
                    {
                        // إنشاء اسم فريد للصورة
                        var fileName = Guid.NewGuid().ToString() + Path.GetExtension(ImageFile.FileName);
                        var imagesFolder = Path.Combine("wwwroot", "images", "news");

                        // إنشاء المجلد إذا لم يكن موجوداً
                        if (!Directory.Exists(imagesFolder))
                        {
                            Directory.CreateDirectory(imagesFolder);
                        }

                        var filePath = Path.Combine(imagesFolder, fileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await ImageFile.CopyToAsync(stream);
                        }

                        news.ImageUrl = "/images/news/" + fileName;
                    }

                    // حفظ معلومات الكاتب
                    var currentUser = await _userManager.GetUserAsync(User);
                    news.AuthorId = currentUser?.Id;
                    news.CreatedAt = DateTime.Now;

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

            return View(news);
        }

        [Authorize(Roles = clsRoles.SuperAdmin)]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var news = await _context.News.FindAsync(id);
            if (news == null)
            {
                return NotFound();
            }

            return View(news);
        }

        [Authorize(Roles = clsRoles.SuperAdmin)]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var news = await _context.News.FindAsync(id);
            if (news == null)
            {
                return NotFound();
            }

            return View(news);
        }

        [Authorize(Roles = clsRoles.SuperAdmin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, News news, IFormFile? ImageFile, bool? removeImage)
        {
            if (id != news.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingNews = await _context.News.FindAsync(id);
                    if (existingNews == null)
                    {
                        return NotFound();
                    }

                    // تحديث الحقول الأساسية
                    existingNews.Title = news.Title;
                    existingNews.Content = news.Content;

                    // التعامل مع الصورة
                    if (removeImage == true)
                    {
                        // حذف الصورة القديمة من السيرفر
                        if (!string.IsNullOrEmpty(existingNews.ImageUrl))
                        {
                            var oldImagePath = Path.Combine("wwwroot", existingNews.ImageUrl.TrimStart('/'));
                            if (System.IO.File.Exists(oldImagePath))
                            {
                                System.IO.File.Delete(oldImagePath);
                            }
                            existingNews.ImageUrl = null;
                        }
                    }
                    else if (ImageFile != null && ImageFile.Length > 0)
                    {
                        // حذف الصورة القديمة أولاً إذا كانت موجودة
                        if (!string.IsNullOrEmpty(existingNews.ImageUrl))
                        {
                            var oldImagePath = Path.Combine("wwwroot", existingNews.ImageUrl.TrimStart('/'));
                            if (System.IO.File.Exists(oldImagePath))
                            {
                                System.IO.File.Delete(oldImagePath);
                            }
                        }

                        // حفظ الصورة الجديدة
                        var fileName = Guid.NewGuid().ToString() + Path.GetExtension(ImageFile.FileName);
                        var imagesFolder = Path.Combine("wwwroot", "images", "news");

                        if (!Directory.Exists(imagesFolder))
                        {
                            Directory.CreateDirectory(imagesFolder);
                        }

                        var filePath = Path.Combine(imagesFolder, fileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await ImageFile.CopyToAsync(stream);
                        }

                        existingNews.ImageUrl = "/images/news/" + fileName;
                    }

                    _context.Update(existingNews);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "✅ تم تحديث الخبر بنجاح!";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!NewsExists(news.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"حدث خطأ: {ex.Message}");
                }
            }

            return View(news);
        }

        [Authorize(Roles = clsRoles.SuperAdmin)]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var news = await _context.News
                .FirstOrDefaultAsync(m => m.Id == id);

            if (news == null)
            {
                return NotFound();
            }

            return View(news);
        }

        [Authorize(Roles = clsRoles.SuperAdmin)]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var news = await _context.News.FindAsync(id);
                if (news != null)
                {
                    // حذف الصورة المرتبطة من السيرفر
                    if (!string.IsNullOrEmpty(news.ImageUrl))
                    {
                        var imagePath = Path.Combine("wwwroot", news.ImageUrl.TrimStart('/'));
                        if (System.IO.File.Exists(imagePath))
                        {
                            System.IO.File.Delete(imagePath);
                        }
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

        // =============== عرض الأخبار للجمهور (بدون تسجيل دخول) ===============

        [AllowAnonymous]
        public async Task<IActionResult> PublicIndex()
        {
            try
            {
                var newsList = await _context.News
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync();
                return View(newsList);
            }
            catch (Exception ex)
            {
                // يمكنك تسجيل الخطأ هنا
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
                var news = await _context.News.FindAsync(id);
                if (news == null)
                {
                    TempData["ErrorMessage"] = "❌ الخبر غير موجود أو تم حذفه";
                    return RedirectToAction(nameof(PublicIndex));
                }

                return View(news);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "❌ حدث خطأ في تحميل الخبر";
                return RedirectToAction(nameof(PublicIndex));
            }
        }

        // =============== دوال مساعدة ===============

        private bool NewsExists(int id)
        {
            return _context.News.Any(e => e.Id == id);
        }

        // دالة لفحص إذا كان الملف صورة
        private bool IsImageFile(IFormFile file)
        {
            if (file == null) return false;

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            return allowedExtensions.Contains(extension);
        }

        // دالة لحساب حجم الصورة المسموح به (مثال: 5MB)
        private bool IsValidFileSize(IFormFile file, long maxSizeInBytes = 5 * 1024 * 1024)
        {
            return file.Length <= maxSizeInBytes;
        }

        // API لجلب الأخبار (اختياري)
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetNews()
        {
            var news = await _context.News
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new
                {
                    n.Id,
                    n.Title,
                    n.Content,
                    n.ImageUrl,
                    n.CreatedAt,
                    CreatedAtFormatted = n.CreatedAt.ToString("yyyy-MM-dd HH:mm")
                })
                .ToListAsync();

            return Json(new { success = true, data = news });
        }

        // دالة لتحميل الصورة فقط (اختياري)
        [HttpPost]
        [Authorize(Roles = clsRoles.SuperAdmin)]
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
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(image.FileName);
                var imagesFolder = Path.Combine("wwwroot", "images", "news");

                if (!Directory.Exists(imagesFolder))
                {
                    Directory.CreateDirectory(imagesFolder);
                }

                var filePath = Path.Combine(imagesFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await image.CopyToAsync(stream);
                }

                var imageUrl = "/images/news/" + fileName;
                return Json(new { success = true, url = imageUrl });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"حدث خطأ: {ex.Message}" });
            }
        }
    }
}