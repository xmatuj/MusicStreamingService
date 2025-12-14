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
                // Получаем текущего пользователя
                var username = User.Identity.Name;
                var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);

                if (currentUser == null)
                {
                    TempData["ErrorMessage"] = "Пользователь не найден";
                    return RedirectToAction("Profile", "Account");
                }

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
                    IsModerated = false,
                    UploadedByUserId = currentUser.Id // Сохраняем ID пользователя
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
                        Comment = "Ожидает модерации",
                        ModerationDate = DateTime.UtcNow
                    };
                    _context.Moderations.Add(moderation);
                    await _context.SaveChangesAsync();
                }

                TempData["SuccessMessage"] = "Трек успешно загружен и отправлен на модерацию!";
                return RedirectToAction("Profile", "Account");
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

        [Authorize(Roles = "Admin")]
        public IActionResult StreamForModeration(int id)
        {
            try
            {
                Console.WriteLine($"=== StreamForModeration called for track {id} ===");

                var track = _context.Tracks.Find(id);
                if (track == null)
                {
                    Console.WriteLine($"Track {id} not found in database");
                    return NotFound();
                }

                // Логируем путь к файлу
                Console.WriteLine($"Track file path: {track.FilePath}");

                // Проверяем разные варианты путей
                var filePath = track.FilePath.StartsWith('/')
                    ? track.FilePath.Substring(1)
                    : track.FilePath;

                var fullPath = Path.Combine(_environment.WebRootPath, filePath);
                Console.WriteLine($"Full path to file: {fullPath}");

                if (!System.IO.File.Exists(fullPath))
                {
                    Console.WriteLine($"File does not exist at: {fullPath}");

                    // Пробуем найти файл в audio папке напрямую
                    var audioFolder = Path.Combine(_environment.WebRootPath, "audio");
                    if (Directory.Exists(audioFolder))
                    {
                        var audioFiles = Directory.GetFiles(audioFolder);
                        Console.WriteLine($"Found {audioFiles.Length} files in audio folder:");
                        foreach (var file in audioFiles)
                        {
                            Console.WriteLine($"  {Path.GetFileName(file)}");
                        }

                        // Попробуем найти файл по имени
                        var fileName = Path.GetFileName(filePath);
                        var directPath = Path.Combine(audioFolder, fileName);
                        if (System.IO.File.Exists(directPath))
                        {
                            Console.WriteLine($"Found file at: {directPath}");
                            fullPath = directPath;
                        }
                        else
                        {
                            return NotFound($"Audio file not found. Expected at: {fullPath}");
                        }
                    }
                    else
                    {
                        return NotFound($"Audio folder not found at: {audioFolder}");
                    }
                }

                Console.WriteLine($"Streaming file from: {fullPath}");

                var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
                var contentType = "audio/mpeg";

                // Определяем тип контента по расширению
                var extension = Path.GetExtension(fullPath).ToLower();
                if (extension == ".mp3") contentType = "audio/mpeg";
                else if (extension == ".wav") contentType = "audio/wav";
                else if (extension == ".ogg") contentType = "audio/ogg";

                Console.WriteLine($"Content-Type: {contentType}");

                // Логируем успешную передачу файла
                Response.OnCompleted(() =>
                {
                    Console.WriteLine($"File streaming completed for track {id}");
                    return Task.CompletedTask;
                });

                return File(fileStream, contentType, enableRangeProcessing: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in StreamForModeration: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
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