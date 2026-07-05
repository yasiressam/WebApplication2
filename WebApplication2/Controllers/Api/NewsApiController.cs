using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;

namespace WebApplication2.Controllers.Api
{
    [Route("api/news")]
    [ApiController]
    public class NewsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly string _newsUploadPath;

        public NewsApiController(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
            _newsUploadPath = Path.Combine("C:\\Users", "Public", "MyApp_Uploads", "News");

            if (!Directory.Exists(_newsUploadPath))
            {
                Directory.CreateDirectory(_newsUploadPath);
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll()
        {
            var news = await _context.News
                .OrderByDescending(n => n.CreatedAt)
                .Take(50)
                .Select(n => new
                {
                    n.Id,
                    n.Title,
                    ShortContent = n.Content.Length > 150 ? n.Content.Substring(0, 150) + "..." : n.Content,
                    n.ImageUrl,
                    n.AuthorName,
                    n.AuthorId,
                    n.CreatedAt,
                    CreatedAtFormatted = n.CreatedAt.ToString("yyyy-MM-dd HH:mm")
                })
                .ToListAsync();

            return Ok(new { success = true, data = news });
        }

        [HttpGet("{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(int id)
        {
            var news = await _context.News.FindAsync(id);

            if (news == null)
            {
                return NotFound(new { success = false, message = "الخبر غير موجود" });
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    news.Id,
                    news.Title,
                    news.Content,
                    news.ImageUrl,
                    news.AuthorName,
                    news.AuthorId,
                    news.CreatedAt,
                    news.UpdatedAt,
                    CreatedAtFormatted = news.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                    UpdatedAtFormatted = news.UpdatedAt?.ToString("yyyy-MM-dd HH:mm")
                }
            });
        }

        [HttpPost]
        [Authorize(Roles = clsRoles.SuperAdmin + "," + clsRoles.NewsEditor)]
        public async Task<IActionResult> Create([FromForm] News model, IFormFile? imageFile)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, errors = ModelState });
            }

            string? imageUrl = null;

            if (imageFile != null && imageFile.Length > 0)
            {
                if (!IsImageFile(imageFile))
                {
                    return BadRequest(new { success = false, message = "الملف ليس صورة صالحة" });
                }

                if (!IsValidFileSize(imageFile))
                {
                    return BadRequest(new { success = false, message = "حجم الصورة أكبر من المسموح (5MB)" });
                }

                imageUrl = await SaveImageAsync(imageFile);
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

            return Ok(new { success = true, message = "تم إضافة الخبر بنجاح", data = news });
        }

        [HttpPut("{id:int}")]
        [Authorize(Roles = clsRoles.SuperAdmin + "," + clsRoles.NewsEditor)]
        public async Task<IActionResult> Update(int id, [FromForm] News model, IFormFile? imageFile, bool removeImage = false)
        {
            var news = await _context.News.FindAsync(id);

            if (news == null)
            {
                return NotFound(new { success = false, message = "الخبر غير موجود" });
            }

            news.Title = model.Title;
            news.Content = model.Content;
            news.UpdatedAt = DateTime.Now;

            if (removeImage && !string.IsNullOrEmpty(news.ImageUrl))
            {
                DeleteImage(news.ImageUrl);
                news.ImageUrl = null;
            }
            else if (imageFile != null && imageFile.Length > 0)
            {
                if (!IsImageFile(imageFile))
                {
                    return BadRequest(new { success = false, message = "الملف ليس صورة صالحة" });
                }

                if (!IsValidFileSize(imageFile))
                {
                    return BadRequest(new { success = false, message = "حجم الصورة أكبر من المسموح (5MB)" });
                }

                if (!string.IsNullOrEmpty(news.ImageUrl))
                {
                    DeleteImage(news.ImageUrl);
                }

                news.ImageUrl = await SaveImageAsync(imageFile);
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "تم تحديث الخبر بنجاح", data = news });
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = clsRoles.SuperAdmin + "," + clsRoles.NewsEditor)]
        public async Task<IActionResult> Delete(int id)
        {
            var news = await _context.News.FindAsync(id);

            if (news == null)
            {
                return NotFound(new { success = false, message = "الخبر غير موجود" });
            }

            if (!string.IsNullOrEmpty(news.ImageUrl))
            {
                DeleteImage(news.ImageUrl);
            }

            _context.News.Remove(news);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "تم حذف الخبر بنجاح" });
        }

        private async Task<string> SaveImageAsync(IFormFile imageFile)
        {
            var fileName = Guid.NewGuid() + Path.GetExtension(imageFile.FileName);
            var filePath = Path.Combine(_newsUploadPath, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await imageFile.CopyToAsync(stream);

            return "/uploads/news/" + fileName;
        }

        private void DeleteImage(string imageUrl)
        {
            var fileName = Path.GetFileName(imageUrl);
            var filePath = Path.Combine(_newsUploadPath, fileName);

            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }

        private static bool IsImageFile(IFormFile file)
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            return allowedExtensions.Contains(extension);
        }

        private static bool IsValidFileSize(IFormFile file, long maxSizeInBytes = 5 * 1024 * 1024)
        {
            return file.Length <= maxSizeInBytes;
        }
    }
}
