using System.ComponentModel.DataAnnotations;

namespace MusicStreamingService.Models
{
    public class Track
    {
        public int Id { get; set; }

        [Required]
        [StringLength(255)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string FilePath { get; set; } = string.Empty;

        public int Duration { get; set; }
        public int GenreId { get; set; }
        public int? AlbumId { get; set; }
        public bool IsModerated { get; set; }
        public int? ArtistId { get; set; }

        public virtual Genre Genre { get; set; } = null!;
        public virtual Album? Album { get; set; }
        public virtual Artist? Artist { get; set; }
        public virtual ICollection<PlaylistTrack> PlaylistTracks { get; set; } = new List<PlaylistTrack>();
        public virtual ICollection<TrackStatistics> Statistics { get; set; } = new List<TrackStatistics>();
        public virtual ICollection<Moderation> Moderations { get; set; } = new List<Moderation>();
    }
}