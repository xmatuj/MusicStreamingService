using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MusicStreamingService.Data;
using MusicStreamingService.Models;

namespace MusicStreamingService.Controllers
{
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Главная страница админки
        public IActionResult Index()
        {
            return View();
        }

        // === УПРАВЛЕНИЕ ЖАНРАМИ ===

        public async Task<IActionResult> Genres()
        {
            var genres = await _context.Genres.ToListAsync();
            return View(genres);
        }

        [HttpPost]
        public async Task<IActionResult> CreateGenre(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                var genre = new Genre { Name = name };
                _context.Genres.Add(genre);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Genres");
        }

        // === УПРАВЛЕНИЕ ИСПОЛНИТЕЛЯМИ ===

        public async Task<IActionResult> Artists()
        {
            var artists = await _context.Artists.ToListAsync();
            return View(artists);
        }

        [HttpPost]
        public async Task<IActionResult> CreateArtist(string name, string description)
        {
            if (!string.IsNullOrEmpty(name))
            {
                var artist = new Artist { Name = name, Description = description };
                _context.Artists.Add(artist);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Artists");
        }

        // === УПРАВЛЕНИЕ АЛЬБОМАМИ ===

        public async Task<IActionResult> Albums()
        {
            var albums = await _context.Albums.Include(a => a.Artist).ToListAsync();
            ViewBag.Artists = await _context.Artists.ToListAsync();
            return View(albums);
        }

        [HttpPost]
        public async Task<IActionResult> CreateAlbum(string title, int artistId, DateTime? releaseDate)
        {
            if (!string.IsNullOrEmpty(title))
            {
                var album = new Album
                {
                    Title = title,
                    ArtistId = artistId,
                    ReleaseDate = releaseDate
                };
                _context.Albums.Add(album);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Albums");
        }

        public IActionResult Moderation()
        {
            return RedirectToAction("Index", "Moderation");
        }
    }
}