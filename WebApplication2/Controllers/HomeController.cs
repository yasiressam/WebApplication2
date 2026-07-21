using Microsoft.AspNetCore.Mvc;
using WebApplication2.Data;
using WebApplication2.Models;
using Microsoft.EntityFrameworkCore;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;

    public HomeController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: Home/Index
    public async Task<IActionResult> Index()
    {
        // جلب الأخبار الأخيرة لتظهر للعامة
        var news = await _context.News
            .AsNoTracking()
            .OrderByDescending(n => n.CreatedAt)
            .Take(20)
            .ToListAsync();
        return View(news);
    }

    // GET: Home/Privacy
    public IActionResult Privacy()
    {
        return View();
    }
}
