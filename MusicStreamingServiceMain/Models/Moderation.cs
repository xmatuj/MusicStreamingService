using System.ComponentModel.DataAnnotations.Schema;

namespace MusicStreamingService.Models
{
    public class Moderation
    {
        public int Id { get; set; }
        public int TrackId { get; set; }
        public int ModeratorId { get; set; }

        [Column(TypeName = "varchar(20)")]
        public ModerationStatus Status { get; set; } = ModerationStatus.Pending;

        public string Comment { get; set; } = string.Empty;
        public DateTime ModerationDate { get; set; } = DateTime.UtcNow;

        public virtual Track Track { get; set; } = null!;
        public virtual User Moderator { get; set; } = null!;
    }

    public enum ModerationStatus
    {
        Approved,
        Rejected,
        Pending
    }
}