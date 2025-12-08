namespace MusicStreamingService.Models
{
    public class PlaylistTrack
    {
        public int PlaylistId { get; set; }
        public int TrackId { get; set; }
        public int Position { get; set; } = 0; // Позиция в плейлисте
        public DateTime AddedDate { get; set; } = DateTime.UtcNow;

        public virtual Playlist Playlist { get; set; } = null!;
        public virtual Track Track { get; set; } = null!;
    }
}