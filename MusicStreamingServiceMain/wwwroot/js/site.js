// Базовая функциональность музыкального плеера
document.addEventListener('DOMContentLoaded', function () {
    const musicPlayer = document.getElementById('musicPlayer');
    const playBtn = document.getElementById('playBtn');
    const prevBtn = document.getElementById('prevBtn');
    const nextBtn = document.getElementById('nextBtn');
    const progressBar = document.querySelector('.progress-bar');
    const progress = document.querySelector('.progress');
    const currentTimeEl = document.querySelector('.current-time');
    const totalTimeEl = document.querySelector('.total-time');

    let isPlaying = false;
    let currentTrack = null;

    // Обработчики для кнопок воспроизведения на треках
    document.querySelectorAll('.play-button').forEach(button => {
        button.addEventListener('click', function (e) {
            e.stopPropagation();
            const trackCard = this.closest('.track-card, .album-card');
            playTrack(trackCard);
        });
    });

    function playTrack(trackCard) {
        const trackTitle = trackCard.querySelector('.track-title, .album-title').textContent;
        const artistName = trackCard.querySelector('.track-artist, .album-artist').textContent;

        // Обновляем информацию в плеере
        document.querySelector('.track-name').textContent = trackTitle;
        document.querySelector('.artist-name').textContent = artistName;

        // Симуляция воспроизведения
        isPlaying = true;
        updatePlayButton();

        // Сброс прогресса
        resetProgress();
    }

    function updatePlayButton() {
        const icon = playBtn.querySelector('i');
        if (isPlaying) {
            icon.className = 'fas fa-pause';
        } else {
            icon.className = 'fas fa-play';
        }
    }

    function resetProgress() {
        progress.style.width = '0%';
        currentTimeEl.textContent = '0:00';
        totalTimeEl.textContent = '3:45'; // Примерное время
    }

    // Обработчики кнопок плеера
    playBtn.addEventListener('click', function () {
        isPlaying = !isPlaying;
        updatePlayButton();
    });

    prevBtn.addEventListener('click', function () {
        // Логика перехода к предыдущему треку
        console.log('Previous track');
    });

    nextBtn.addEventListener('click', function () {
        // Логика перехода к следующему треку
        console.log('Next track');
    });

    // Прогресс-бар
    progressBar.addEventListener('click', function (e) {
        const rect = this.getBoundingClientRect();
        const percent = (e.clientX - rect.left) / rect.width;
        progress.style.width = (percent * 100) + '%';

        // Обновление времени
        const totalSeconds = 225; // 3:45 в секундах
        const currentSeconds = Math.floor(totalSeconds * percent);
        currentTimeEl.textContent = formatTime(currentSeconds);
    });

    function formatTime(seconds) {
        const mins = Math.floor(seconds / 60);
        const secs = seconds % 60;
        return `${mins}:${secs.toString().padStart(2, '0')}`;
    }

    // Поиск
    const searchForm = document.querySelector('.search-form');
    const searchInput = document.querySelector('.search-input');

    searchForm.addEventListener('submit', function (e) {
        e.preventDefault();
        const query = searchInput.value.trim();
        if (query) {
            // В будущем здесь будет поиск
            console.log('Searching for:', query);
            window.location.href = `/Search?query=${encodeURIComponent(query)}`;
        }
    });
});