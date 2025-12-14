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

        // НОВОЕ: Свойство для получения обложки трека (из альбома или артиста)
        [NotMapped]
        public string CoverImage
        {
            get
            {
                // Если есть альбом и у него есть обложка - используем её
                if (Album != null && !string.IsNullOrEmpty(Album.CoverPath))
                {
                    return Album.CoverPath;
                }

                // Если есть артист и у него есть фото - используем его
                if (Artist != null && !string.IsNullOrEmpty(Artist.PhotoPath))
                {
                    return Artist.PhotoPath;
                }

                // Если ничего нет - возвращаем дефолтную картинку
                return "/images/default-track-cover.jpg";
            }
        }

        // Свойство для получения цвета на основе ID трека (для placeholder)
        [NotMapped]
        public string ColorHash
        {
            get
            {
                var colors = new[] { "#667eea", "#764ba2", "#f093fb", "#f5576c", "#4facfe", "#00f2fe", "#43e97b", "#38f9d7" };
                return colors[Id % colors.Length];
            }
        }
    }
}