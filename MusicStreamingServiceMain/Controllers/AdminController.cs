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
        private readonly IWebHostEnvironment _environment;

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
        public async Task<IActionResult> CreateArtist(string name, string description, IFormFile? photoFile)
        {
            if (!string.IsNullOrEmpty(name))
            {
                string? photoPath = null;

                // Обработка загрузки фото
                if (photoFile != null && photoFile.Length > 0)
                {
                    photoPath = await SaveImageAsync(photoFile, "artists");
                }

                var artist = new Artist
                {
                    Name = name,
                    Description = description,
                    PhotoPath = photoPath
                };
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
        public async Task<IActionResult> CreateAlbum(string title, int artistId, DateTime? releaseDate, IFormFile? coverFile)
        {
            if (!string.IsNullOrEmpty(title))
            {
                string? coverPath = null;

                // Обработка загрузки обложки
                if (coverFile != null && coverFile.Length > 0)
                {
                    coverPath = await SaveImageAsync(coverFile, "albums");
                }

                var album = new Album
                {
                    Title = title,
                    ArtistId = artistId,
                    ReleaseDate = releaseDate,
                    CoverPath = coverPath
                };
                _context.Albums.Add(album);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Albums");
        }

        // Вспомогательный метод для сохранения изображений
        private async Task<string?> SaveImageAsync(IFormFile imageFile, string folderName)
        {
            try
            {
                // Проверяем тип файла
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var fileExtension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    return null;
                }

                // Создаем уникальное имя файла
                var fileName = Guid.NewGuid().ToString() + fileExtension;
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "images", folderName);

                // Создаем папку, если её нет
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var filePath = Path.Combine(uploadsFolder, fileName);

                // Сохраняем файл
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(fileStream);
                }

                // Возвращаем путь относительно wwwroot
                return $"/images/{folderName}/{fileName}";
            }
            catch (Exception)
            {
                return null;
            }
        }

    }
}