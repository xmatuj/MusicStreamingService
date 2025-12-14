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

        // Добавим вспомогательные свойства для placeholder цветов
        public string GetAlbumColor(int albumId)
        {
            var colors = new[] { "#667eea", "#764ba2", "#f093fb", "#f5576c", "#4facfe", "#00f2fe", "#43e97b", "#38f9d7" };
            return colors[albumId % colors.Length];
        }

        public string GetArtistColor(int artistId)
        {
            var colors = new[] { "#fa709a", "#ff6a00", "#f093fb", "#30cfd0", "#a3bded", "#6991c7", "#fad0c4", "#a1c4fd" };
            return colors[artistId % colors.Length];
        }
    }
}