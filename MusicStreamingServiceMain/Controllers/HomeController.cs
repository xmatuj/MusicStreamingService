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
            var viewModel = new HomeViewModel
            {
                PopularTracks = await _context.Tracks
                    .Include(t => t.Artist)
                    .Include(t => t.Album)
                    .Include(t => t.Genre)
                    .Where(t => t.IsModerated)
                    .OrderByDescending(t => t.Statistics.Sum(s => s.ListenCount))
                    .Take(10)
                    .ToListAsync(),

                NewReleases = await _context.Albums
                    .Include(a => a.Artist)
                    .Include(a => a.Tracks)
                    .Where(a => a.ReleaseDate.HasValue && a.ReleaseDate.Value >= DateTime.Now.AddMonths(-1))
                    .OrderByDescending(a => a.ReleaseDate)
                    .Take(8)
                    .ToListAsync(),

                FeaturedArtists = await _context.Artists
                    .Include(a => a.Albums)
                    .OrderByDescending(a => a.Albums.Sum(al => al.Statistics.Sum(s => s.ListenCount)))
                    .Take(6)
                    .ToListAsync()
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