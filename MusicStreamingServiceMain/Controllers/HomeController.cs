using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MusicStreamingService.Data;
using MusicStreamingService.Models;
using MusicStreamingServiceMain.Models;
using System.Diagnostics;

namespace MusicStreamingService.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // 1. Отключаем кэширование браузера, чтобы при нажатии F5 запрос шел на сервер
            Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
            Response.Headers.Append("Pragma", "no-cache");
            Response.Headers.Append("Expires", "0");

            // 2. Получаем треки из базы в память
            var allTracks = await _context.Tracks
                .Include(t => t.Artist)
                .Include(t => t.Album)
                .Include(t => t.Genre)
                .Where(t => t.IsModerated)
                .ToListAsync();

            // 3. Перемешиваем список в памяти с помощью C# Random
            var random = new Random();
            var shuffledTracks = allTracks
                .OrderBy(x => random.Next()) // случайная сортировка
                .Take(15) // Берем 15 штук для ленты
                .ToList();

            // 4. То же самое для исполнителей
            var allArtists = await _context.Artists
                .Include(a => a.Albums)
                .ToListAsync();

            var shuffledArtists = allArtists
                .OrderBy(x => random.Next())
                .Take(10)
                .ToList();

            var viewModel = new HomeViewModel
            {
                PopularTracks = shuffledTracks,

                // Новинки оставляем по дате
                NewReleases = await _context.Albums
                    .Include(a => a.Artist)
                    .Include(a => a.Tracks)
                    .Where(a => a.ReleaseDate.HasValue && a.ReleaseDate.Value >= DateTime.Now.AddMonths(-1))
                    .OrderByDescending(a => a.ReleaseDate)
                    .Take(10)
                    .ToListAsync(),

                FeaturedArtists = shuffledArtists
            };

            return View(viewModel);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }

    public class HomeViewModel
    {
        public List<Track> PopularTracks { get; set; }
        public List<Album> NewReleases { get; set; }
        public List<Artist> FeaturedArtists { get; set; }
    }
}