using System.ComponentModel.DataAnnotations;

namespace MusicStreamingService.Models
{
    public class PlaylistCreateViewModel
    {
        [Required(ErrorMessage = "Название плейлиста обязательно")]
        [StringLength(255, MinimumLength = 2, ErrorMessage = "Название должно быть от 2 до 255 символов")]
        [Display(Name = "Название плейлиста")]
        public string Title { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "Описание не должно превышать 1000 символов")]
        [Display(Name = "Описание (необязательно)")]
        public string? Description { get; set; }

        [Display(Name = "Видимость")]
        public PlaylistVisibility Visibility { get; set; } = PlaylistVisibility.Private;

        [Display(Name = "Сделать плейлист публичным")]
        public bool IsPublic { get; set; } = false;
    }

    public class PlaylistEditViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Название плейлиста обязательно")]
        [StringLength(255, MinimumLength = 2, ErrorMessage = "Название должно быть от 2 до 255 символов")]
        [Display(Name = "Название плейлиста")]
        public string Title { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "Описание не должно превышать 1000 символов")]
        [Display(Name = "Описание (необязательно)")]
        public string? Description { get; set; }

        [Display(Name = "Видимость")]
        public PlaylistVisibility Visibility { get; set; } = PlaylistVisibility.Private;
    }

    public class AddToPlaylistViewModel
    {
        public int TrackId { get; set; }
        public string TrackTitle { get; set; } = string.Empty;
        public string ArtistName { get; set; } = string.Empty;
        public List<Playlist> UserPlaylists { get; set; } = new List<Playlist>();
        public bool CanCreatePlaylist { get; set; }
    }
}