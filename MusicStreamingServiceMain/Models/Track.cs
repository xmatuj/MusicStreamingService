using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

        // Добавляем поле для хранения ID пользователя, который загрузил трек
        public int? UploadedByUserId { get; set; }

        public virtual Genre Genre { get; set; } = null!;
        public virtual Album? Album { get; set; }
        public virtual Artist? Artist { get; set; }
        public virtual User? UploadedByUser { get; set; } // Связь с пользователем
        public virtual ICollection<PlaylistTrack> PlaylistTracks { get; set; } = new List<PlaylistTrack>();
        public virtual ICollection<TrackStatistics> Statistics { get; set; } = new List<TrackStatistics>();
        public virtual ICollection<Moderation> Moderations { get; set; } = new List<Moderation>();

        // Свойство для получения последней модерации
        [NotMapped]
        public Moderation? LatestModeration => Moderations?.OrderByDescending(m => m.ModerationDate).FirstOrDefault();

        // Свойство для получения статуса модерации
        [NotMapped]
        public string ModerationStatus
        {
            get
            {
                if (!IsModerated && LatestModeration == null)
                    return "На модерации";

                if (LatestModeration != null)
                {
                    var status = LatestModeration.Status.ToString();
                    if (status == "Approved") return "Одобрен";
                    if (status == "Rejected") return "Отклонен";
                    if (status == "Pending") return "На модерации";
                }

                return "Неизвестно";
            }
        }

        // Свойство для получения комментария модерации
        [NotMapped]
        public string ModerationComment => LatestModeration?.Comment ?? "";
    }
}