using System.ComponentModel.DataAnnotations;

namespace MusicStreamingService.Models
{
    public class Album
    {
        public int Id { get; set; }

        [Required]
        [StringLength(255)]
        public string Title { get; set; } = string.Empty;

        public int ArtistId { get; set; }
        public DateTime? ReleaseDate { get; set; }

        public virtual Artist Artist { get; set; } = null!;
        public virtual ICollection<Track> Tracks { get; set; } = new List<Track>();
        public virtual ICollection<AlbumStatistics> Statistics { get; set; } = new List<AlbumStatistics>();
    }
}