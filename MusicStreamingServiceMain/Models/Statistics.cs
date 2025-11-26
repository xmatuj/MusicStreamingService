namespace MusicStreamingService.Models
{
    public class TrackStatistics
    {
        public int Id { get; set; }
        public int TrackId { get; set; }
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public int ListenCount { get; set; }

        public virtual Track Track { get; set; } = null!;
    }

    public class AlbumStatistics
    {
        public int Id { get; set; }
        public int AlbumId { get; set; }
        public DateTime Date { get; set; } = DateTime.UtcNow;
        public int ListenCount { get; set; }

        public virtual Album Album { get; set; } = null!;
    }
}