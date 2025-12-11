using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MusicStreamingService.Data;
using MusicStreamingService.Models;
using System.Security.Claims;

namespace MusicStreamingService.Controllers
{
    [Authorize]
    public class PlaylistsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PlaylistsController> _logger;

        public PlaylistsController(ApplicationDbContext context, ILogger<PlaylistsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Проверка может ли пользователь создавать плейлисты
        private bool CanUserCreatePlaylists(User user)
        {
            return user.Role == UserRole.Admin ||
                   user.Role == UserRole.Musician ||
                   (user.Subscriptions != null && user.Subscriptions.Any(s => s.IsActivated && s.EndDate > DateTime.Now));
        }

        // Получение текущего пользователя
        private async Task<User?> GetCurrentUserAsync()
        {
            try
            {
                var username = User.Identity?.Name;
                Console.WriteLine($"=== GetCurrentUserAsync ===");
                Console.WriteLine($"User.Identity.Name: {username}");
                Console.WriteLine($"User.Identity.IsAuthenticated: {User.Identity?.IsAuthenticated}");

                if (string.IsNullOrEmpty(username))
                {
                    Console.WriteLine("ERROR: Username is null or empty");
                    return null;
                }

                var user = await _context.Users
                    .Include(u => u.Subscriptions)
                    .FirstOrDefaultAsync(u => u.Username == username);

                Console.WriteLine($"User found in DB: {(user != null ? user.Id.ToString() : "null")}");

                if (user != null)
                {
                    Console.WriteLine($"User ID: {user.Id}, Username: {user.Username}, Role: {user.Role}");
                }

                return user;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"EXCEPTION in GetCurrentUserAsync: {ex.Message}");
                return null;
            }
        }

        // GET: /Playlists - Мои плейлисты
        public async Task<IActionResult> Index()
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
                return RedirectToAction("Login", "Account");

            var playlists = await _context.Playlists
                .Where(p => p.UserId == user.Id)
                .Include(p => p.PlaylistTracks)
                .ThenInclude(pt => pt.Track)
                .OrderByDescending(p => p.CreatedDate)
                .ToListAsync();

            ViewBag.CanCreatePlaylists = CanUserCreatePlaylists(user);
            return View(playlists);
        }

        // GET: /Playlists/Create - Создание плейлиста
        public async Task<IActionResult> Create()
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
                return RedirectToAction("Login", "Account");

            if (!CanUserCreatePlaylists(user))
            {
                TempData["ErrorMessage"] = "Для создания плейлистов требуется подписка. Пожалуйста, приобретите подписку.";
                return RedirectToAction("Profile", "Account");
            }

            return View(new PlaylistCreateViewModel());
        }

        // POST: /Playlists/Create - Создание плейлиста
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PlaylistCreateViewModel model)
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
                return RedirectToAction("Login", "Account");

            if (!CanUserCreatePlaylists(user))
            {
                TempData["ErrorMessage"] = "Для создания плейлистов требуется подписка. Пожалуйста, приобретите подписку.";
                return RedirectToAction("Profile", "Account");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var playlist = new Playlist
                    {
                        Title = model.Title,
                        Description = model.Description,
                        UserId = user.Id,
                        Visibility = model.IsPublic ? PlaylistVisibility.Public : PlaylistVisibility.Private,
                        CreatedDate = DateTime.UtcNow
                    };

                    _context.Playlists.Add(playlist);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Пользователь {Username} создал плейлист '{PlaylistTitle}'", user.Username, playlist.Title);
                    TempData["SuccessMessage"] = "Плейлист успешно создан!";

                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при создании плейлиста");
                    ModelState.AddModelError("", "Произошла ошибка при создании плейлиста. Попробуйте позже.");
                }
            }

            return View(model);
        }

        // GET: /Playlists/Edit/{id} - Редактирование плейлиста
        public async Task<IActionResult> Edit(int id)
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
                return RedirectToAction("Login", "Account");

            var playlist = await _context.Playlists
                .FirstOrDefaultAsync(p => p.Id == id && p.UserId == user.Id);

            if (playlist == null)
                return NotFound();

            var model = new PlaylistEditViewModel
            {
                Id = playlist.Id,
                Title = playlist.Title,
                Description = playlist.Description,
                Visibility = playlist.Visibility
            };

            return View(model);
        }

        // POST: /Playlists/Edit/{id} - Редактирование плейлиста
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PlaylistEditViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            var user = await GetCurrentUserAsync();
            if (user == null)
                return RedirectToAction("Login", "Account");

            var playlist = await _context.Playlists
                .FirstOrDefaultAsync(p => p.Id == id && p.UserId == user.Id);

            if (playlist == null)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    playlist.Title = model.Title;
                    playlist.Description = model.Description;
                    playlist.Visibility = model.Visibility;
                    playlist.UpdatedDate = DateTime.UtcNow;

                    _context.Playlists.Update(playlist);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Пользователь {Username} обновил плейлист '{PlaylistTitle}'", user.Username, playlist.Title);
                    TempData["SuccessMessage"] = "Плейлист успешно обновлен!";

                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при обновлении плейлиста");
                    ModelState.AddModelError("", "Произошла ошибка при обновлении плейлиста. Попробуйте позже.");
                }
            }

            return View(model);
        }

        // GET: /Playlists/Details/{id} - Детали плейлиста
        public async Task<IActionResult> Details(int id)
        {
            var playlist = await _context.Playlists
                .Include(p => p.User)
                .Include(p => p.PlaylistTracks)
                .ThenInclude(pt => pt.Track)
                .ThenInclude(t => t.Artist)
                .Include(p => p.PlaylistTracks)
                .ThenInclude(pt => pt.Track)
                .ThenInclude(t => t.Album)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (playlist == null)
                return NotFound();

            // Проверяем доступ
            var user = await GetCurrentUserAsync();
            if (playlist.Visibility == PlaylistVisibility.Private &&
                (user == null || playlist.UserId != user.Id))
            {
                return Forbid();
            }

            return View(playlist);
        }

        // POST: /Playlists/Delete/{id} - Удаление плейлиста
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
                return RedirectToAction("Login", "Account");

            var playlist = await _context.Playlists
                .FirstOrDefaultAsync(p => p.Id == id && p.UserId == user.Id);

            if (playlist == null)
                return NotFound();

            try
            {
                _context.Playlists.Remove(playlist);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Пользователь {Username} удалил плейлист '{PlaylistTitle}'", user.Username, playlist.Title);
                TempData["SuccessMessage"] = "Плейлист успешно удален!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении плейлиста");
                TempData["ErrorMessage"] = "Произошла ошибка при удалении плейлиста.";
            }

            return RedirectToAction("Index");
        }

        // GET: /Playlists/AddToPlaylistModal - Модальное окно добавления трека в плейлист
        public async Task<IActionResult> AddToPlaylistModal(int trackId)
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
                return Unauthorized();

            var track = await _context.Tracks
                .Include(t => t.Artist)
                .FirstOrDefaultAsync(t => t.Id == trackId && t.IsModerated);

            if (track == null)
                return NotFound();

            var playlists = await _context.Playlists
                .Where(p => p.UserId == user.Id)
                .Include(p => p.PlaylistTracks)
                .OrderByDescending(p => p.CreatedDate)
                .ToListAsync();

            var model = new AddToPlaylistViewModel
            {
                TrackId = trackId,
                TrackTitle = track.Title,
                ArtistName = track.Artist?.Name ?? "Неизвестный исполнитель",
                UserPlaylists = playlists,
                CanCreatePlaylist = CanUserCreatePlaylists(user)
            };

            return PartialView("_AddToPlaylistModal", model);
        }

        // POST: /Playlists/AddTrack - Добавление трека (традиционная форма)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddTrack(int playlistId, int trackId, string returnUrl = null)
        {
            Console.WriteLine($"=== TRADITIONAL FORM AddTrack ===");
            Console.WriteLine($"playlistId: {playlistId}, trackId: {trackId}, returnUrl: {returnUrl}");

            var user = await GetCurrentUserAsync();
            if (user == null)
            {
                TempData["ErrorMessage"] = "Пользователь не найден";
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var playlist = await _context.Playlists
                    .Include(p => p.PlaylistTracks)
                    .FirstOrDefaultAsync(p => p.Id == playlistId && p.UserId == user.Id);

                if (playlist == null)
                {
                    TempData["ErrorMessage"] = "Плейлист не найден";
                    // Возвращаем на предыдущую страницу или главную
                    return Redirect(returnUrl ?? "/");
                }

                var track = await _context.Tracks
                    .FirstOrDefaultAsync(t => t.Id == trackId && t.IsModerated);

                if (track == null)
                {
                    TempData["ErrorMessage"] = "Трек не найден";
                    return Redirect(returnUrl ?? "/");
                }

                // Проверяем, не добавлен ли уже
                var existingEntry = playlist.PlaylistTracks
                    .FirstOrDefault(pt => pt.TrackId == trackId);

                if (existingEntry != null)
                {
                    TempData["ErrorMessage"] = "Этот трек уже добавлен в плейлист";
                    return Redirect(returnUrl ?? "/");
                }

                // Добавляем
                var maxPosition = playlist.PlaylistTracks.Any()
                    ? playlist.PlaylistTracks.Max(pt => pt.Position)
                    : -1;

                var playlistTrack = new PlaylistTrack
                {
                    PlaylistId = playlistId,
                    TrackId = trackId,
                    Position = maxPosition + 1,
                    AddedDate = DateTime.UtcNow
                };

                _context.PlaylistTracks.Add(playlistTrack);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Трек '{track.Title}' добавлен в плейлист '{playlist.Title}'";

                // Возвращаем на предыдущую страницу или главную
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                return RedirectToAction("Index", "Home"); // Или на главную
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
                TempData["ErrorMessage"] = "Произошла ошибка при добавлении трека";
                return Redirect(returnUrl ?? "/");
            }
        }

        // POST: /Playlists/RemoveTrack (традиционная форма)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveTrack(int playlistId, int trackId)
        {
            Console.WriteLine($"=== RemoveTrack (Traditional) START ===");

            var user = await GetCurrentUserAsync();
            if (user == null)
                return RedirectToAction("Login", "Account");

            try
            {
                var playlist = await _context.Playlists
                    .FirstOrDefaultAsync(p => p.Id == playlistId && p.UserId == user.Id);

                if (playlist == null)
                {
                    TempData["ErrorMessage"] = "Плейлист не найден";
                    return RedirectToAction("Details", new { id = playlistId });
                }

                var playlistTrack = await _context.PlaylistTracks
                    .FirstOrDefaultAsync(pt => pt.PlaylistId == playlistId && pt.TrackId == trackId);

                if (playlistTrack != null)
                {
                    _context.PlaylistTracks.Remove(playlistTrack);
                    await _context.SaveChangesAsync();
                    await UpdatePlaylistPositions(playlistId);

                    TempData["SuccessMessage"] = "Трек удален из плейлиста";
                }
                else
                {
                    TempData["ErrorMessage"] = "Трек не найден в плейлисте";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
                TempData["ErrorMessage"] = "Произошла ошибка при удалении трека";
            }

            return RedirectToAction("Details", new { id = playlistId });
        }

        // POST: /Playlists/Reorder - Изменение порядка треков в плейлисте
        [HttpPost]
        public async Task<IActionResult> Reorder(int playlistId, List<int> trackIds)
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
                return Unauthorized();

            var playlist = await _context.Playlists
                .FirstOrDefaultAsync(p => p.Id == playlistId && p.UserId == user.Id);

            if (playlist == null)
                return NotFound();

            try
            {
                var playlistTracks = await _context.PlaylistTracks
                    .Where(pt => pt.PlaylistId == playlistId)
                    .ToListAsync();

                for (int i = 0; i < trackIds.Count; i++)
                {
                    var playlistTrack = playlistTracks.FirstOrDefault(pt => pt.TrackId == trackIds[i]);
                    if (playlistTrack != null)
                    {
                        playlistTrack.Position = i;
                    }
                }

                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при изменении порядка треков в плейлисте");
                return Json(new { success = false, message = "Произошла ошибка при изменении порядка" });
            }
        }

        // Вспомогательный метод для обновления позиций треков
        private async Task UpdatePlaylistPositions(int playlistId)
        {
            try
            {
                Console.WriteLine($"=== UpdatePlaylistPositions for playlist {playlistId} ===");

                var playlistTracks = await _context.PlaylistTracks
                    .Where(pt => pt.PlaylistId == playlistId)
                    .OrderBy(pt => pt.Position)
                    .ToListAsync();

                Console.WriteLine($"Found {playlistTracks.Count} tracks in playlist");

                for (int i = 0; i < playlistTracks.Count; i++)
                {
                    Console.WriteLine($"Updating position: trackId={playlistTracks[i].TrackId}, oldPosition={playlistTracks[i].Position}, newPosition={i}");
                    playlistTracks[i].Position = i;
                }

                await _context.SaveChangesAsync();
                Console.WriteLine("Positions updated successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in UpdatePlaylistPositions: {ex.Message}");
                throw;
            }
        }

        // GET: /Playlists/CheckPermission - Проверка прав пользователя
        [HttpGet]
        public async Task<IActionResult> CheckPermission()
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
            {
                return Json(new { canCreate = false });
            }

            var canCreate = CanUserCreatePlaylists(user);
            return Json(new { canCreate });
        }
    }
}