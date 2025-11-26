using System.ComponentModel.DataAnnotations;

namespace MusicStreamingService.Models
{
    public class Genre
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public virtual ICollection<Track> Tracks { get; set; } = new List<Track>();
    }
}