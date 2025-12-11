using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MusicStreamingService.Data;
using MusicStreamingService.Models;

namespace MusicStreamingService.Controllers
{
    public class SearchController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SearchController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Search?query=...
        public async Task<IActionResult> Index(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.SearchQuery = query;

            // Ищем треки по названию или исполнителю
            var tracks = await _context.Tracks
                .Include(t => t.Artist)
                .Include(t => t.Album)
                .Include(t => t.Genre)
                .Where(t => t.IsModerated &&
                    (t.Title.Contains(query) ||
                     t.Artist.Name.Contains(query)))
                .OrderByDescending(t => t.Statistics.Sum(s => s.ListenCount))
                .ToListAsync();

            // Ищем исполнителей
            var artists = await _context.Artists
                .Where(a => a.Name.Contains(query))
                .Take(10)
                .ToListAsync();

            // Ищем альбомы
            var albums = await _context.Albums
                .Include(a => a.Artist)
                .Where(a => a.Title.Contains(query) ||
                          a.Artist.Name.Contains(query))
                .Take(10)
                .ToListAsync();

            var viewModel = new SearchViewModel
            {
                Query = query,
                Tracks = tracks,
                Artists = artists,
                Albums = albums,
                TotalResults = tracks.Count + artists.Count + albums.Count
            };

            return View(viewModel);
        }
    }

    public class SearchViewModel
    {
        public string Query { get; set; }
        public List<Track> Tracks { get; set; }
        public List<Artist> Artists { get; set; }
        public List<Album> Albums { get; set; }
        public int TotalResults { get; set; }
    }
}