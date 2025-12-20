// MusicStreamingServiceMain/Controllers/StatisticsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MusicStreamingService.Data;
using MusicStreamingService.Models;

namespace MusicStreamingService.Controllers
{
    [Authorize(Roles = "Admin")]
    public class StatisticsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StatisticsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Statistics/Tracks - Статистика треков
        public async Task<IActionResult> Tracks(string search = "", string period = "all")
        {
            var query = _context.TrackStatistics
                .Include(ts => ts.Track)
                    .ThenInclude(t => t.Artist)
                .Include(ts => ts.Track)
                    .ThenInclude(t => t.Album)
                .AsQueryable();

            // Фильтр по поиску
            if (!string.IsNullOrEmpty(search))
            {
                // Используем EF.Functions.Like для поиска без учета регистра
                query = query.Where(ts =>
                    EF.Functions.Like(ts.Track.Title, $"%{search}%") ||
                    EF.Functions.Like(ts.Track.Artist.Name, $"%{search}%"));
            }

            // Фильтр по периоду
            var now = DateTime.UtcNow;
            switch (period.ToLower())
            {
                case "today":
                    query = query.Where(ts => ts.Date.Date == now.Date);
                    break;
                case "week":
                    query = query.Where(ts => ts.Date >= now.AddDays(-7));
                    break;
                case "month":
                    query = query.Where(ts => ts.Date >= now.AddMonths(-1));
                    break;
                case "year":
                    query = query.Where(ts => ts.Date >= now.AddYears(-1));
                    break;
                    // "all" - без фильтра
            }

            // Группируем по треку для получения общей статистики
            var trackStats = await query
                .GroupBy(ts => new {
                    ts.TrackId,
                    TrackTitle = ts.Track.Title,
                    ArtistName = ts.Track.Artist.Name,
                    AlbumTitle = ts.Track.Album != null ? ts.Track.Album.Title : "Без альбома"
                })
                .Select(g => new TrackStatViewModel
                {
                    TrackId = g.Key.TrackId,
                    TrackTitle = g.Key.TrackTitle,
                    ArtistName = g.Key.ArtistName,
                    AlbumTitle = g.Key.AlbumTitle,
                    TotalListens = g.Sum(ts => ts.ListenCount),
                    LastListen = g.Max(ts => ts.Date)
                })
                .OrderByDescending(ts => ts.TotalListens)
                .ToListAsync();

            // Если есть поиск, фильтруем результаты на клиенте
            if (!string.IsNullOrEmpty(search))
            {
                var searchLower = search.ToLower();
                trackStats = trackStats.Where(ts =>
                    ts.TrackTitle.ToLower().Contains(searchLower) ||
                    ts.ArtistName.ToLower().Contains(searchLower) ||
                    ts.AlbumTitle.ToLower().Contains(searchLower))
                    .ToList();
            }

            ViewBag.Search = search;
            ViewBag.Period = period;
            ViewBag.Periods = new Dictionary<string, string>
            {
                { "all", "За все время" },
                { "today", "Сегодня" },
                { "week", "За неделю" },
                { "month", "За месяц" },
                { "year", "За год" }
            };

            return View(trackStats);
        }

        // GET: /Statistics/Albums - Статистика альбомов (агрегируем из треков)
        public async Task<IActionResult> Albums(string search = "", string period = "all")
        {
            // Получаем статистику альбомов из статистики треков
            var trackStatsQuery = _context.TrackStatistics
                .Include(ts => ts.Track)
                    .ThenInclude(t => t.Artist)
                .Include(ts => ts.Track)
                    .ThenInclude(t => t.Album)
                .Where(ts => ts.Track.AlbumId != null) // Только треки с альбомами
                .AsQueryable();

            // Фильтр по периоду
            var now = DateTime.UtcNow;
            switch (period.ToLower())
            {
                case "today":
                    trackStatsQuery = trackStatsQuery.Where(ts => ts.Date.Date == now.Date);
                    break;
                case "week":
                    trackStatsQuery = trackStatsQuery.Where(ts => ts.Date >= now.AddDays(-7));
                    break;
                case "month":
                    trackStatsQuery = trackStatsQuery.Where(ts => ts.Date >= now.AddMonths(-1));
                    break;
                case "year":
                    trackStatsQuery = trackStatsQuery.Where(ts => ts.Date >= now.AddYears(-1));
                    break;
                    // "all" - без фильтра
            }

            // Группируем по альбому для получения общей статистики
            var albumStats = await trackStatsQuery
                .GroupBy(ts => new {
                    AlbumId = ts.Track.AlbumId.Value,
                    AlbumTitle = ts.Track.Album.Title,
                    ArtistName = ts.Track.Album.Artist.Name
                })
                .Select(g => new AlbumStatViewModel
                {
                    AlbumId = g.Key.AlbumId,
                    AlbumTitle = g.Key.AlbumTitle,
                    ArtistName = g.Key.ArtistName,
                    TotalListens = g.Sum(ts => ts.ListenCount),
                    LastListen = g.Max(ts => ts.Date),
                    TrackCount = g.Select(ts => ts.TrackId).Distinct().Count() // Количество уникальных треков
                })
                .OrderByDescending(as_ => as_.TotalListens)
                .ToListAsync();

            // Также получаем альбомы без статистики (для отображения всех альбомов)
            var allAlbums = await _context.Albums
                .Include(a => a.Artist)
                .ToListAsync();

            // Объединяем статистику с информацией об альбомах
            var result = allAlbums
                .Select(album => new AlbumStatViewModel
                {
                    AlbumId = album.Id,
                    AlbumTitle = album.Title,
                    ArtistName = album.Artist.Name,
                    TotalListens = albumStats
                        .FirstOrDefault(as_ => as_.AlbumId == album.Id)?.TotalListens ?? 0,
                    LastListen = albumStats
                        .FirstOrDefault(as_ => as_.AlbumId == album.Id)?.LastListen ?? DateTime.MinValue,
                    TrackCount = albumStats
                        .FirstOrDefault(as_ => as_.AlbumId == album.Id)?.TrackCount ?? 0
                })
                .OrderByDescending(a => a.TotalListens)
                .ThenBy(a => a.AlbumTitle)
                .ToList();

            // Применяем поиск на клиенте (после получения всех данных)
            if (!string.IsNullOrEmpty(search))
            {
                var searchLower = search.ToLower();
                result = result.Where(a =>
                    a.AlbumTitle.ToLower().Contains(searchLower) ||
                    a.ArtistName.ToLower().Contains(searchLower))
                    .ToList();
            }

            ViewBag.Search = search;
            ViewBag.Period = period;
            ViewBag.Periods = new Dictionary<string, string>
            {
                { "all", "За все время" },
                { "today", "Сегодня" },
                { "week", "За неделю" },
                { "month", "За месяц" },
                { "year", "За год" }
            };

            return View(result);
        }

        // GET: /Statistics/Details/{id} - Детальная статистика трека
        public async Task<IActionResult> TrackDetails(int id, string period = "all")
        {
            var track = await _context.Tracks
                .Include(t => t.Artist)
                .Include(t => t.Album)
                .Include(t => t.Genre)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (track == null)
                return NotFound();

            var query = _context.TrackStatistics
                .Where(ts => ts.TrackId == id);

            // Фильтр по периоду
            var now = DateTime.UtcNow;
            switch (period.ToLower())
            {
                case "today":
                    query = query.Where(ts => ts.Date.Date == now.Date);
                    break;
                case "week":
                    query = query.Where(ts => ts.Date >= now.AddDays(-7));
                    break;
                case "month":
                    query = query.Where(ts => ts.Date >= now.AddMonths(-1));
                    break;
                case "year":
                    query = query.Where(ts => ts.Date >= now.AddYears(-1));
                    break;
                    // "all" - без фильтра
            }

            var dailyStats = await query
                .GroupBy(ts => ts.Date.Date)
                .Select(g => new DailyStatViewModel
                {
                    Date = g.Key,
                    ListenCount = g.Sum(ts => ts.ListenCount)
                })
                .OrderByDescending(ds => ds.Date)
                .Take(30) // Последние 30 дней
                .ToListAsync();

            var totalListens = await query.SumAsync(ts => ts.ListenCount);
            var firstListen = await query.MinAsync(ts => (DateTime?)ts.Date);
            var lastListen = await query.MaxAsync(ts => (DateTime?)ts.Date);

            var viewModel = new TrackDetailStatViewModel
            {
                Track = track,
                TotalListens = totalListens,
                FirstListen = firstListen,
                LastListen = lastListen,
                DailyStats = dailyStats,
                Period = period
            };

            ViewBag.Periods = new Dictionary<string, string>
            {
                { "all", "За все время" },
                { "today", "Сегодня" },
                { "week", "За неделю" },
                { "month", "За месяц" },
                { "year", "За год" }
            };

            return View(viewModel);
        }
    }

    // Модели для представлений
    public class TrackStatViewModel
    {
        public int TrackId { get; set; }
        public string TrackTitle { get; set; } = string.Empty;
        public string ArtistName { get; set; } = string.Empty;
        public string AlbumTitle { get; set; } = string.Empty;
        public int TotalListens { get; set; }
        public DateTime LastListen { get; set; }
    }

    public class AlbumStatViewModel
    {
        public int AlbumId { get; set; }
        public string AlbumTitle { get; set; } = string.Empty;
        public string ArtistName { get; set; } = string.Empty;
        public int TotalListens { get; set; }
        public DateTime LastListen { get; set; }
        public int TrackCount { get; set; } // Количество треков в альбоме
    }

    public class DailyStatViewModel
    {
        public DateTime Date { get; set; }
        public int ListenCount { get; set; }
    }

    public class TrackDetailStatViewModel
    {
        public Track Track { get; set; } = null!;
        public int TotalListens { get; set; }
        public DateTime? FirstListen { get; set; }
        public DateTime? LastListen { get; set; }
        public List<DailyStatViewModel> DailyStats { get; set; } = new List<DailyStatViewModel>();
        public string Period { get; set; } = "all";
    }
}