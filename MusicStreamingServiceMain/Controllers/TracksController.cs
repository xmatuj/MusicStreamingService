using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MusicStreamingService.Data;
using MusicStreamingService.Models;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;

namespace MusicStreamingService.Controllers
{
    public class TracksController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public TracksController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }
        // Похожие треки
        public async Task<IActionResult> Similar(int genreId, int currentTrackId)
        {
            var similarTracks = await _context.Tracks
                .Include(t => t.Artist)
                .Where(t => t.GenreId == genreId && t.Id != currentTrackId && t.IsModerated)
                .OrderByDescending(t => t.Statistics.Sum(s => s.ListenCount))
                .Take(5)
                .ToListAsync();

            return PartialView("_SimilarTracks", similarTracks);
        }

        // GET: Страница добавления трека
        public async Task<IActionResult> Create()
        {
            // Проверяем авторизацию
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Account");
            }

            // Проверяем может ли пользователь добавлять треки
            var username = User.Identity.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);

            if (user == null || !user.CanUploadTracks)
            {
                TempData["ErrorMessage"] = "У вас нет прав для добавления треков. Только музыканты и администраторы могут загружать треки.";
                return RedirectToAction("Profile", "Account");
            }

            ViewBag.Artists = await _context.Artists.ToListAsync();
            ViewBag.Genres = await _context.Genres.ToListAsync();
            ViewBag.Albums = await _context.Albums.ToListAsync();
            return View();
        }

        // POST: Добавление трека
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TrackCreateViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Сохранение аудиофайла
                var audioFileName = await SaveAudioFile(model.AudioFile);

                var track = new Track
                {
                    Title = model.Title,
                    FilePath = $"/audio/{audioFileName}",
                    Duration = model.Duration,
                    GenreId = model.GenreId,
                    AlbumId = model.AlbumId,
                    ArtistId = model.ArtistId,
                    IsModerated = false // Требует модерации
                };

                _context.Tracks.Add(track);
                await _context.SaveChangesAsync();

                // Находим первого администратора для модерации
                var admin = await _context.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Admin);

                if (admin != null)
                {
                    // Создаем запрос на модерацию
                    var moderation = new Moderation
                    {
                        TrackId = track.Id,
                        ModeratorId = admin.Id, // Используем реального администратора
                        Status = ModerationStatus.Pending,
                        Comment = "Ожидает модерации"
                    };
                    _context.Moderations.Add(moderation);
                    await _context.SaveChangesAsync();
                }

                return RedirectToAction("Index", "Home");
            }

            ViewBag.Artists = await _context.Artists.ToListAsync();
            ViewBag.Genres = await _context.Genres.ToListAsync();
            ViewBag.Albums = await _context.Albums.ToListAsync();
            return View(model);
        }

        // GET: Воспроизведение трека
        public async Task<IActionResult> Play(int id)
        {
            var track = await _context.Tracks
                .Include(t => t.Artist)
                .Include(t => t.Album)
                .Include(t => t.Genre)
                .FirstOrDefaultAsync(t => t.Id == id && t.IsModerated);

            if (track == null)
                return NotFound();

            // Проверяем авторизацию и возможность добавления в плейлисты
            var user = await GetCurrentUserAsync();
            ViewBag.CanAddToPlaylists = user != null &&
                (user.Role == UserRole.Admin ||
                 user.Role == UserRole.Musician ||
                 user.Role == UserRole.Subscriber);

            // Логируем прослушивание
            var stat = new TrackStatistics
            {
                TrackId = id,
                ListenCount = 1
            };
            _context.TrackStatistics.Add(stat);
            await _context.SaveChangesAsync();

            return View(track);
        }

        private async Task<User?> GetCurrentUserAsync()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
                return null;

            return await _context.Users
                .Include(u => u.Subscriptions)
                .FirstOrDefaultAsync(u => u.Username == username);
        }

        // Потоковая передача аудио
        public IActionResult Stream(int id)
        {
            var track = _context.Tracks.Find(id);
            if (track == null)
                return NotFound();

            // Модераторы должны иметь доступ ко всем трекам, даже немодерированным
            var filePath = Path.Combine(_environment.WebRootPath, track.FilePath.TrimStart('/'));

            if (!System.IO.File.Exists(filePath))
                return NotFound();

            var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            return File(fileStream, "audio/mpeg", enableRangeProcessing: true);
        }

        private async Task<string> SaveAudioFile(IFormFile audioFile)
        {
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "audio");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = Guid.NewGuid().ToString() + "_" + audioFile.FileName;
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await audioFile.CopyToAsync(fileStream);
            }

            return uniqueFileName;
        }

        // POST: Запись прослушивания трека
        [HttpPost]
        public async Task<IActionResult> RecordPlay(int id)
        {
            var track = await _context.Tracks.FindAsync(id);
            if (track == null || !track.IsModerated)
                return NotFound();

            // Добавляем запись в статистику
            var stat = new TrackStatistics
            {
                TrackId = id,
                ListenCount = 1
            };
            _context.TrackStatistics.Add(stat);
            await _context.SaveChangesAsync();

            return Ok();
        }
    }

    public class TrackCreateViewModel
    {
        [Required]
        public string Title { get; set; } = string.Empty;

        [Required]
        public IFormFile AudioFile { get; set; }

        [Required]
        [Range(1, 3600, ErrorMessage = "Длительность должна быть от 1 до 3600 секунд")]
        public int Duration { get; set; }

        [Required]
        public int GenreId { get; set; }

        public int? AlbumId { get; set; }

        [Required]
        public int ArtistId { get; set; }
    }
}