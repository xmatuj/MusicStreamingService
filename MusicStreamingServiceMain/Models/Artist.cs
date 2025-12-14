using System.ComponentModel.DataAnnotations;

namespace MusicStreamingService.Models
{
    public class Artist
    {
        public int Id { get; set; }

        [Required]
        [StringLength(255)]
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        // Добавляем поле для изображения
        [StringLength(500)]
        public string? PhotoPath { get; set; }

        public virtual ICollection<Album> Albums { get; set; } = new List<Album>();
        public virtual ICollection<Track> Tracks { get; set; } = new List<Track>();
    }
}