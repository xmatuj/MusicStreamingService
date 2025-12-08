using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MusicStreamingService.Models
{
    public class Playlist
    {
        public int Id { get; set; }

        [Required]
        [StringLength(255)]
        public string Title { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        public int UserId { get; set; }

        [Column(TypeName = "varchar(20)")]
        public PlaylistVisibility Visibility { get; set; } = PlaylistVisibility.Private;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedDate { get; set; }

        [StringLength(255)]
        public string? CoverImagePath { get; set; }

        public virtual User User { get; set; } = null!;
        public virtual ICollection<PlaylistTrack> PlaylistTracks { get; set; } = new List<PlaylistTrack>();

        // Свойство для количества треков
        [NotMapped]
        public int TrackCount => PlaylistTracks?.Count ?? 0;

        // Свойство для общей длительности
        [NotMapped]
        public int TotalDuration => PlaylistTracks?.Sum(pt => pt.Track?.Duration ?? 0) ?? 0;
    }

    public enum PlaylistVisibility
    {
        Private,
        Public,
        Unlisted
    }
}