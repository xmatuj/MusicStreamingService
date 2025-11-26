using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MusicStreamingService.Data;
using MusicStreamingService.Models;

namespace MusicStreamingService.Controllers
{
    public class ModerationController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ModerationController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Список треков на модерации
        public async Task<IActionResult> Index()
        {
            var pendingTracks = await _context.Tracks
                .Include(t => t.Artist)
                .Include(t => t.Genre)
                .Include(t => t.Album)
                .Where(t => !t.IsModerated)
                .ToListAsync();

            return View(pendingTracks);
        }

        // GET: Детали трека для модерации
        public async Task<IActionResult> Review(int id)
        {
            var track = await _context.Tracks
                .Include(t => t.Artist)
                .Include(t => t.Genre)
                .Include(t => t.Album)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (track == null)
                return NotFound();

            return View(track);
        }

        // POST: Одобрить трек
        [HttpPost]
        public async Task<IActionResult> Approve(int id, string comment)
        {
            var track = await _context.Tracks.FindAsync(id);
            if (track == null)
                return NotFound();

            // Находим текущего модератора (администратора)
            var moderator = await _context.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Admin);

            if (moderator != null)
            {
                // Обновляем статус трека
                track.IsModerated = true;

                // Создаем или обновляем запись модерации
                var moderation = await _context.Moderations
                    .FirstOrDefaultAsync(m => m.TrackId == id);

                if (moderation == null)
                {
                    moderation = new Moderation
                    {
                        TrackId = id,
                        ModeratorId = moderator.Id,
                        Status = ModerationStatus.Approved,
                        Comment = comment ?? "Трек одобрен",
                        ModerationDate = DateTime.UtcNow
                    };
                    _context.Moderations.Add(moderation);
                }
                else
                {
                    moderation.Status = ModerationStatus.Approved;
                    moderation.Comment = comment ?? "Трек одобрен";
                    moderation.ModerationDate = DateTime.UtcNow;
                    moderation.ModeratorId = moderator.Id;
                }

                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }

        // POST: Отклонить трек
        [HttpPost]
        public async Task<IActionResult> Reject(int id, string comment)
        {
            var track = await _context.Tracks.FindAsync(id);
            if (track == null)
                return NotFound();

            // Находим текущего модератора
            var moderator = await _context.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Admin);

            if (moderator != null)
            {
                // Создаем или обновляем запись модерации
                var moderation = await _context.Moderations
                    .FirstOrDefaultAsync(m => m.TrackId == id);

                if (moderation == null)
                {
                    moderation = new Moderation
                    {
                        TrackId = id,
                        ModeratorId = moderator.Id,
                        Status = ModerationStatus.Rejected,
                        Comment = comment ?? "Трек отклонен",
                        ModerationDate = DateTime.UtcNow
                    };
                    _context.Moderations.Add(moderation);
                }
                else
                {
                    moderation.Status = ModerationStatus.Rejected;
                    moderation.Comment = comment ?? "Трек отклонен";
                    moderation.ModerationDate = DateTime.UtcNow;
                    moderation.ModeratorId = moderator.Id;
                }

                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }

        // GET: История модераций
        public async Task<IActionResult> History()
        {
            var moderations = await _context.Moderations
                .Include(m => m.Track)
                .Include(m => m.Moderator)
                .OrderByDescending(m => m.ModerationDate)
                .ToListAsync();

            return View(moderations);
        }
    }
}