using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MusicStreamingService.Data;
using MusicStreamingService.Models;

namespace MusicStreamingService.Controllers
{
    [Authorize(Roles = "Admin")]
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

        // GET: /Admin/Users - Список всех пользователей
        public async Task<IActionResult> Users(string search = "")
        {
            // Сначала получаем всех пользователей
            var users = await _context.Users
                .OrderBy(u => u.Role)
                .ThenBy(u => u.Username)
                .ToListAsync();

            // Если есть поисковый запрос - фильтруем
            if (!string.IsNullOrEmpty(search))
            {
                users = users.Where(u =>
                    u.Username.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    u.Email.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    u.Role.ToString().Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    u.Id.ToString().Contains(search)
                ).ToList();
            }

            ViewBag.Search = search;
            return View("Users", users);
        }

        // POST: /Admin/ChangeRole/{id}
        [HttpPost]
        public async Task<IActionResult> ChangeRole(int id, UserRole newRole)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            user.Role = newRole;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Роль пользователя {user.Username} изменена на {newRole}";
            return RedirectToAction("Users");
        }

        [HttpPost]
        public async Task<IActionResult> SetAsMusician(int id)
        {
            return await ChangeRole(id, UserRole.Musician);
        }

        [HttpPost]
        public async Task<IActionResult> SetAsAdmin(int id)
        {
            return await ChangeRole(id, UserRole.Admin);
        }

        [HttpPost]
        public async Task<IActionResult> SetAsUser(int id)
        {
            return await ChangeRole(id, UserRole.User);
        }

        [HttpPost]
        public async Task<IActionResult> SetAsSubscriber(int id)
        {
            return await ChangeRole(id, UserRole.Subscriber);
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