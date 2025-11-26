using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MusicStreamingService.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(255)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "varchar(20)")]
        public UserRole Role { get; set; }

        public DateTime DateOfCreated { get; set; } = DateTime.UtcNow;

        public virtual ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
        public virtual ICollection<Playlist> Playlists { get; set; } = new List<Playlist>();

        // Методы для работы с паролями
        public void SetPassword(string password)
        {
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
        }

        public bool VerifyPassword(string password)
        {
            return BCrypt.Net.BCrypt.Verify(password, PasswordHash);
        }
    }

    public enum UserRole
    {
        User,
        Subscriber,
        Musician,
        Admin
    }

    // Отдельный класс для системных ролей (не хранятся в БД)
    public static class SystemRoles
    {
        public const string Guest = "Guest";
    }
}