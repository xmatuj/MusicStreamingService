// Простой музыкальный плеер с рекламой
let currentAudio = null;
let isPlaying = false;

document.addEventListener('DOMContentLoaded', function () {
    console.log('Music player initialized');

    // Обработчики для кнопок воспроизведения на треках
    document.querySelectorAll('.play-button').forEach(button => {
        button.addEventListener('click', function (e) {
            e.stopPropagation();
            const trackCard = this.closest('.track-card, .album-card');

            if (trackCard) {
                console.log('Play button clicked');
                handlePlayClick(trackCard);
            }
        });
    });

    // Инициализация элементов плеера
    initPlayerControls();
});

// Обработка клика на воспроизведение
function handlePlayClick(trackCard) {
    if (!trackCard) {
        console.error('Карточка трека не найдена');
        return;
    }

    // Получаем информацию о треке
    const trackId = trackCard.dataset.trackId;
    const trackTitle = trackCard.querySelector('.track-title, .album-title')?.textContent || 'Неизвестный трек';
    const artistName = trackCard.querySelector('.track-artist, .album-artist')?.textContent || 'Неизвестный исполнитель';

    console.log('Воспроизведение трека:', trackTitle, 'исполнитель:', artistName, 'ID:', trackId);

    // Обновляем информацию в плеере ДО показа рекламы
    updatePlayerInfo(trackTitle, artistName);

    // Проверяем нужно ли показывать рекламу
    if (window.shouldShowAd === true) {
        console.log('Показываем рекламу для пользователя без подписки');
        showAdAndPlay(trackId, trackTitle, artistName, trackCard);
    } else {
        console.log('Пропускаем рекламу для подписчика/администратора/музыканта');
        playTrackDirectly(trackId, trackTitle, artistName, trackCard);
    }
}

// Показать рекламу и затем воспроизвести
function showAdAndPlay(trackId, trackTitle, artistName, trackCard) {
    const adModal = new bootstrap.Modal(document.getElementById('advertisementModal'));
    const timerElement = document.getElementById('adTimer');
    const closeBtn = document.querySelector('#advertisementModal .modal-footer .btn');

    let secondsLeft = 5;
    let timerInterval;

    // Сохраняем данные трека для использования после рекламы
    const trackData = { trackId, trackTitle, artistName, trackCard };

    // Обновляем таймер
    function updateTimer() {
        if (timerElement) {
            timerElement.textContent = secondsLeft;
        }

        if (closeBtn) {
            closeBtn.textContent = secondsLeft > 0 ? `Закрыть через ${secondsLeft}с` : 'Закрыть';
            closeBtn.disabled = secondsLeft > 0;
        }

        if (secondsLeft <= 0) {
            clearInterval(timerInterval);
        }
    }

    // Обработчик закрытия рекламы
    function onAdClosed() {
        console.log('Реклама завершена, воспроизводим трек');
        playTrackDirectly(trackData.trackId, trackData.trackTitle, trackData.artistName, trackData.trackCard);
    }

    // Настраиваем кнопку закрытия
    if (closeBtn) {
        closeBtn.onclick = function () {
            if (secondsLeft <= 0) {
                adModal.hide();
                onAdClosed();
            }
        };
    }

    // Запускаем таймер
    updateTimer();
    timerInterval = setInterval(() => {
        secondsLeft--;
        updateTimer();
    }, 1000);

    // Обработчик когда модальное окно скрыто
    document.getElementById('advertisementModal').addEventListener('hidden.bs.modal', function onHidden() {
        clearInterval(timerInterval);

        // Вызываем только если реклама просмотрена полностью
        if (secondsLeft <= 0) {
            onAdClosed();
        }

        // Удаляем обработчик
        this.removeEventListener('hidden.bs.modal', onHidden);
    });

    // Показываем рекламу
    adModal.show();
}

// Непосредственное воспроизведение трека
function playTrackDirectly(trackId, trackTitle, artistName, trackCard) {
    console.log('Непосредственное воспроизведение трека:', trackTitle);

    // Обновляем информацию в плеере (на всякий случай)
    updatePlayerInfo(trackTitle, artistName);

    // Показываем плеер
    showPlayer();

    // Начинаем воспроизведение
    startPlayback(trackId, trackCard);
}

// Обновить информацию в плеере
function updatePlayerInfo(trackTitle, artistName) {
    console.log('Updating player info:', trackTitle, '-', artistName);

    let trackNameEl = document.getElementById('currentTrackName');
    let artistNameEl = document.getElementById('currentArtistName');

    if (trackNameEl) {
        console.log('Found track name element:', trackNameEl);
        trackNameEl.textContent = trackTitle;
    } else {
        console.warn('Track name element not found');
        // Попробуем найти в плеере
        const player = document.getElementById('musicPlayer');
        if (player) {
            const trackEl = player.querySelector('.track-name, [class*="track"], #currentTrackName');
            if (trackEl) {
                trackEl.textContent = trackTitle;
                console.log('Found track element in player');
            }
        }
    }

    if (artistNameEl) {
        console.log('Found artist name element:', artistNameEl);
        artistNameEl.textContent = artistName;
    } else {
        console.warn('Artist name element not found');
        // Попробуем найти в плеере
        const player = document.getElementById('musicPlayer');
        if (player) {
            const artistEl = player.querySelector('.artist-name, [class*="artist"], #currentArtistName');
            if (artistEl) {
                artistEl.textContent = artistName;
                console.log('Found artist element in player');
            }
        }
    }
}

// Показать плеер
function showPlayer() {
    const musicPlayer = document.getElementById('musicPlayer');
    if (musicPlayer) {
        musicPlayer.style.display = 'flex';
        console.log('Player shown');
    }
}

// Начать воспроизведение
function startPlayback(trackId, trackCard) {
    console.log('Начинаем воспроизведение трека:', trackId);

    // Останавливаем текущее воспроизведение
    if (currentAudio) {
        currentAudio.pause();
        currentAudio = null;
    }

    // Сбрасываем пометку у всех треков
    document.querySelectorAll('.track-card[data-playing="true"], .album-card[data-playing="true"]').forEach(card => {
        card.removeAttribute('data-playing');
    });

    // Помечаем текущий трек как играющий
    if (trackCard) {
        trackCard.setAttribute('data-playing', 'true');

        // Добавляем визуальное выделение
        trackCard.style.boxShadow = '0 0 10px rgba(0, 123, 255, 0.5)';
        trackCard.style.transition = 'box-shadow 0.3s';

        // Убираем выделение у других треков
        document.querySelectorAll('.track-card, .album-card').forEach(card => {
            if (card !== trackCard) {
                card.style.boxShadow = '';
            }
        });
    }

    if (trackId && trackId !== 'undefined') {
        try {
            currentAudio = new Audio(`/Tracks/Stream/${trackId}`);

            currentAudio.volume = 0.7; // Устанавливаем громкость

            currentAudio.addEventListener('canplay', function () {
                console.log('Аудио готово к воспроизведению');
                currentAudio.play()
                    .then(() => {
                        console.log('Воспроизведение начато успешно');
                        isPlaying = true;
                        updatePlayButtonState();
                    })
                    .catch(error => {
                        console.error('Ошибка воспроизведения:', error);
                        simulatePlayback();
                    });
            });

            currentAudio.addEventListener('error', function (e) {
                console.error('Ошибка аудио:', e);
                simulatePlayback();
            });

            currentAudio.addEventListener('timeupdate', updateProgress);

            currentAudio.addEventListener('ended', function () {
                console.log('Трек завершен, переходим к следующему');
                isPlaying = false;
                updatePlayButtonState();

                // Автоматически переходим к следующему треку
                setTimeout(() => {
                    playNextTrack();
                }, 1000);
            });

        } catch (error) {
            console.error('Ошибка создания аудио:', error);
            simulatePlayback();
        }
    } else {
        console.warn('Нет ID трека, симулируем воспроизведение');
        simulatePlayback();
    }
}

// Симуляция воспроизведения (если нет реального трека)
function simulatePlayback() {
    const progress = document.querySelector('.progress');
    const currentTimeEl = document.querySelector('.current-time');
    const totalTimeEl = document.querySelector('.total-time');

    if (progress) progress.style.width = '0%';
    if (currentTimeEl) currentTimeEl.textContent = '0:00';
    if (totalTimeEl) totalTimeEl.textContent = '3:30';

    let currentTime = 0;
    const totalTime = 210; // 3:30 в секундах

    const simulateInterval = setInterval(() => {
        if (!isPlaying) {
            clearInterval(simulateInterval);
            return;
        }

        currentTime += 1;
        const progressPercent = (currentTime / totalTime) * 100;

        if (progress) progress.style.width = progressPercent + '%';
        if (currentTimeEl) currentTimeEl.textContent = formatTime(currentTime);

        if (currentTime >= totalTime) {
            clearInterval(simulateInterval);
            isPlaying = false;
            updatePlayButton();
        }
    }, 1000);
}

// Обновить прогресс воспроизведения
function updateProgress() {
    if (!currentAudio) return;

    const progress = document.querySelector('.progress');
    const currentTimeEl = document.querySelector('.current-time');
    const totalTimeEl = document.querySelector('.total-time');

    const currentTime = currentAudio.currentTime;
    const duration = currentAudio.duration || 210;
    const progressPercent = (currentTime / duration) * 100;

    if (progress) progress.style.width = progressPercent + '%';
    if (currentTimeEl) currentTimeEl.textContent = formatTime(currentTime);
    if (totalTimeEl) totalTimeEl.textContent = formatTime(duration);
}

// Форматировать время
function formatTime(seconds) {
    const mins = Math.floor(seconds / 60);
    const secs = Math.floor(seconds % 60);
    return `${mins}:${secs.toString().padStart(2, '0')}`;
}

// Инициализация кнопок управления плеером
function initPlayerControls() {
    console.log('Initializing player controls...');

    // Ищем кнопки разными способами
    const playBtn = document.getElementById('playBtn') ||
        document.querySelector('.play-btn, [class*="play-btn"], .control-btn.play-btn');

    const prevBtn = document.getElementById('prevBtn') ||
        document.querySelector('[onclick*="previous"], .control-btn:first-child');

    const nextBtn = document.getElementById('nextBtn') ||
        document.querySelector('[onclick*="next"], .control-btn:last-child');

    const volumeIcon = document.getElementById('volumeIcon');
    const volumeSlider = document.getElementById('volumeSlider');

    console.log('Play button:', playBtn ? 'FOUND' : 'NOT FOUND');
    console.log('Prev button:', prevBtn ? 'FOUND' : 'NOT FOUND');
    console.log('Next button:', nextBtn ? 'FOUND' : 'NOT FOUND');

    // Обработчик кнопки play/pause
    if (playBtn) {
        playBtn.onclick = function (e) {
            e.preventDefault();
            e.stopPropagation();

            if (!currentAudio) {
                console.log('No audio to play');
                return;
            }

            if (isPlaying) {
                currentAudio.pause();
            } else {
                currentAudio.play().catch(e => {
                    console.error('Play failed:', e);
                });
            }
            isPlaying = !isPlaying;
            updatePlayButtonState();
        };

        // Также добавляем обработчик для иконки внутри кнопки
        const playIcon = playBtn.querySelector('i');
        if (playIcon) {
            playIcon.onclick = function (e) {
                e.stopPropagation();
                playBtn.click();
            };
        }
    }

    // Обработчик предыдущего трека
    if (prevBtn) {
        prevBtn.onclick = function (e) {
            e.preventDefault();
            e.stopPropagation();
            console.log('Previous track');
            // Реализация перехода к предыдущему треку
            playPreviousTrack();
        };
    }

    // Обработчик следующего трека
    if (nextBtn) {
        nextBtn.onclick = function (e) {
            e.preventDefault();
            e.stopPropagation();
            console.log('Next track');
            // Реализация перехода к следующему треку
            playNextTrack();
        };
    }

    // Обработчик громкости
    if (volumeIcon) {
        volumeIcon.onclick = function (e) {
            e.preventDefault();
            e.stopPropagation();
            toggleMute();
        };
    }

    if (volumeSlider) {
        volumeSlider.oninput = function (e) {
            e.preventDefault();
            setVolume(this.value);
        };
    }

    // Обработчик прогресс-бара
    const progressBar = document.querySelector('.progress-bar, .player-progress, [onclick*="seek"]');
    if (progressBar) {
        progressBar.onclick = function (e) {
            e.preventDefault();
            e.stopPropagation();

            if (!currentAudio) return;

            const rect = this.getBoundingClientRect();
            const percent = (e.clientX - rect.left) / rect.width;
            const duration = currentAudio.duration || 210;

            currentAudio.currentTime = percent * duration;
            updateProgress();
        };
    }
}

function updatePlayButtonState() {
    console.log('Updating play button, isPlaying:', isPlaying);

    // Ищем кнопку разными способами
    const playBtn = document.getElementById('playBtn') ||
        document.querySelector('.play-btn, [class*="play-btn"]');

    if (!playBtn) {
        console.warn('Play button not found for update');
        return;
    }

    const icon = playBtn.querySelector('i');
    if (!icon) {
        console.warn('Play icon not found');
        return;
    }

    if (isPlaying) {
        icon.className = 'fas fa-pause';
        console.log('Changed to pause icon');
    } else {
        icon.className = 'fas fa-play';
        console.log('Changed to play icon');
    }
}

// Управление громкостью
function setVolume(value) {
    if (currentAudio) {
        currentAudio.volume = value / 100;
        console.log('Volume set to:', value);
    }
}

function toggleMute() {
    if (!currentAudio) return;

    currentAudio.muted = !currentAudio.muted;
    const volumeIcon = document.getElementById('volumeIcon') ||
        document.querySelector('[onclick*="toggleMute"] i');

    if (volumeIcon) {
        volumeIcon.className = currentAudio.muted ? 'fas fa-volume-mute' : 'fas fa-volume-up';
        console.log('Mute toggled:', currentAudio.muted);
    }
}

// Переход к предыдущему/следующему треку
// Получить все треки на странице
function getAllTracks() {
    const trackCards = Array.from(document.querySelectorAll('.track-card, .album-card'));
    return trackCards.map((card, index) => ({
        element: card,
        index: index,
        trackId: card.dataset.trackId,
        title: card.querySelector('.track-title, .album-title')?.textContent || `Трек ${index + 1}`,
        artist: card.querySelector('.track-artist, .album-artist')?.textContent || 'Неизвестный исполнитель'
    }));
}

// Найти текущий играющий трек
function getCurrentPlayingTrack() {
    const currentCard = document.querySelector('.track-card[data-playing="true"], .album-card[data-playing="true"]');
    if (currentCard) {
        const tracks = getAllTracks();
        return tracks.find(track => track.element === currentCard);
    }
    return null;
}

function playPreviousTrack() {
    console.log('Переход к предыдущему треку');

    const tracks = getAllTracks();
    if (tracks.length === 0) {
        console.log('Нет треков на странице');
        return;
    }

    const currentTrack = getCurrentPlayingTrack();
    let previousIndex;

    if (currentTrack) {
        // Если есть текущий трек, берем предыдущий
        previousIndex = currentTrack.index - 1;
        if (previousIndex < 0) {
            // Если первый трек, переходим к последнему
            previousIndex = tracks.length - 1;
        }
    } else {
        // Если нет текущего трека, начинаем с последнего
        previousIndex = tracks.length - 1;
    }

    const previousTrack = tracks[previousIndex];
    console.log('Переходим к треку:', previousTrack.title, 'индекс:', previousIndex);

    // Воспроизводим выбранный трек
    handlePlayClick(previousTrack.element);
}

function playNextTrack() {
    console.log('Переход к следующему треку');

    const tracks = getAllTracks();
    if (tracks.length === 0) {
        console.log('Нет треков на странице');
        return;
    }

    const currentTrack = getCurrentPlayingTrack();
    let nextIndex;

    if (currentTrack) {
        // Если есть текущий трек, берем следующий
        nextIndex = currentTrack.index + 1;
        if (nextIndex >= tracks.length) {
            // Если последний трек, переходим к первому
            nextIndex = 0;
        }
    } else {
        // Если нет текущего трека, начинаем с первого
        nextIndex = 0;
    }

    const nextTrack = tracks[nextIndex];
    console.log('Переходим к треку:', nextTrack.title, 'индекс:', nextIndex);

    // Воспроизводим выбранный трек
    handlePlayClick(nextTrack.element);
}


// Глобальные функции для HTML onclick атрибутов
window.togglePlay = function () {
    if (!currentAudio) return;

    if (isPlaying) {
        currentAudio.pause();
    } else {
        currentAudio.play();
    }
    isPlaying = !isPlaying;
    updatePlayButtonState();
};

window.previousTrack = function () {
    playPreviousTrack();
};

window.nextTrack = function () {
    playNextTrack();
};

window.toggleMuteGlobal = function () {
    toggleMute();
};

window.setVolumeGlobal = function (value) {
    setVolume(value);
};

window.seek = function (event) {
    if (!currentAudio) return;

    const progressBar = event.currentTarget;
    const rect = progressBar.getBoundingClientRect();
    const percent = (event.clientX - rect.left) / rect.width;
    const duration = currentAudio.duration || 210;

    currentAudio.currentTime = percent * duration;
    updateProgress();
};