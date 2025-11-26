using System.ComponentModel.DataAnnotations;

namespace MusicStreamingService.Models
{
    public class Playlist
    {
        public int Id { get; set; }

        [Required]
        [StringLength(255)]
        public string Title { get; set; } = string.Empty;

        public int UserId { get; set; }
        public DateTime DateOfCreated { get; set; } = DateTime.UtcNow;

        public virtual User User { get; set; } = null!;
        public virtual ICollection<PlaylistTrack> PlaylistTracks { get; set; } = new List<PlaylistTrack>();
    }
}