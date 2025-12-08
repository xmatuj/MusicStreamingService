document.addEventListener('DOMContentLoaded', function () {
    console.log('Playlist script loaded');

    // Обработка кликов по кнопкам добавления в плейлист
    document.addEventListener('click', function (e) {
        const addBtn = e.target.closest('.add-to-playlist-btn');
        if (addBtn) {
            e.preventDefault();
            e.stopPropagation();

            const trackId = addBtn.dataset.trackId;
            console.log('Opening modal for track:', trackId);

            fetch(`/Playlists/AddToPlaylistModal?trackId=${trackId}`)
                .then(r => r.text())
                .then(html => {
                    console.log('Modal HTML loaded');

                    const div = document.createElement('div');
                    div.innerHTML = html;
                    document.body.appendChild(div.firstElementChild);

                    const modalEl = document.getElementById('addToPlaylistModal');
                    if (modalEl) {
                        const modal = new bootstrap.Modal(modalEl);
                        modal.show();

                        // ОТЛАДКА: Проверим все data-атрибуты
                        console.log('=== MODAL DEBUG ===');
                        const playlistItems = modalEl.querySelectorAll('.playlist-item');
                        playlistItems.forEach((item, index) => {
                            console.log(`Playlist ${index}:`, {
                                element: item,
                                dataset: item.dataset,
                                playlistId: item.dataset.playlistId,
                                innerHTML: item.innerHTML
                            });
                        });

                        // Обработчик кликов ВНУТРИ модального окна
                        modalEl.addEventListener('click', function (modalEvent) {
                            // Кнопка плейлиста
                            const playlistItem = modalEvent.target.closest('.playlist-item');
                            if (playlistItem) {
                                modalEvent.preventDefault();
                                modalEvent.stopPropagation();

                                // ВАЖНО: Правильно получаем playlistId
                                const playlistId = playlistItem.getAttribute('data-playlist-id') ||
                                    playlistItem.dataset.playlistId;
                                const trackId = modalEl.querySelector('#modalTrackId')?.value;

                                console.log('=== CLICK ON PLAYLIST ===');
                                console.log('Playlist item:', playlistItem);
                                console.log('Playlist ID (from getAttribute):', playlistItem.getAttribute('data-playlist-id'));
                                console.log('Playlist ID (from dataset):', playlistItem.dataset.playlistId);
                                console.log('All data attributes:', playlistItem.dataset);
                                console.log('Track ID:', trackId);

                                if (!playlistId || playlistId === '0' || playlistId === 'undefined') {
                                    alert('Ошибка: Не удалось получить ID плейлиста');
                                    console.error('Invalid playlistId:', playlistId);
                                    return;
                                }

                                // Преобразуем в число
                                const playlistIdNum = parseInt(playlistId);
                                const trackIdNum = parseInt(trackId);

                                console.log('Parsed IDs:', { playlistIdNum, trackIdNum });

                                // Визуальная обратная связь
                                const originalContent = playlistItem.innerHTML;
                                playlistItem.innerHTML = '<div style="text-align: center;"><i class="fas fa-spinner fa-spin"></i> Добавление...</div>';
                                playlistItem.style.pointerEvents = 'none';

                                // Отправляем запрос
                                addTrackToPlaylist(playlistIdNum, trackIdNum, playlistItem, modal);
                            }

                            // Кнопка создания нового плейлиста
                            if (modalEvent.target.closest('#createNewPlaylistBtn')) {
                                modalEvent.preventDefault();
                                modalEvent.stopPropagation();

                                const trackId = modalEl.querySelector('#modalTrackId').value;
                                modal.hide();

                                setTimeout(() => {
                                    window.location.href = `/Playlists/Create?trackId=${trackId}`;
                                }, 300);
                            }
                        });
                    }
                })
                .catch(err => {
                    console.error('Error:', err);
                    alert('Ошибка загрузки модального окна');
                });
        }
    });

    // Функция добавления трека
    async function addTrackToPlaylist(playlistId, trackId, playlistElement, modalInstance) {
        console.log(`=== addTrackToPlaylist CALLED ===`);
        console.log(`playlistId: ${playlistId}, type: ${typeof playlistId}`);
        console.log(`trackId: ${trackId}, type: ${typeof trackId}`);

        if (!playlistId || playlistId === 0 || isNaN(playlistId)) {
            alert('Ошибка: Неверный ID плейлиста');
            console.error('Invalid playlistId:', playlistId);
            return;
        }

        // Получаем токен
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

        try {
            const response = await fetch('/Playlists/AddTrack', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token || ''
                },
                body: JSON.stringify({
                    playlistId: playlistId,
                    trackId: trackId
                })
            });

            console.log('Response status:', response.status);
            const responseText = await response.text();
            console.log('Response text:', responseText);

            let data;
            try {
                data = JSON.parse(responseText);
            } catch (e) {
                console.error('Failed to parse JSON:', e);
                alert('Ошибка сервера: неверный формат ответа');
                return;
            }

            console.log('Response data:', data);

            if (data.success) {
                // Успех
                playlistElement.innerHTML = `<div style="text-align: center; color: green;">
                    <i class="fas fa-check"></i> ${data.message}
                </div>`;

                setTimeout(() => {
                    modalInstance.hide();
                    alert(data.message);
                }, 1500);
            } else {
                // Ошибка
                alert(`Ошибка: ${data.message}`);
                // Восстанавливаем кнопку
                playlistElement.style.pointerEvents = 'auto';
                playlistElement.innerHTML = `<div style="text-align: center; color: red;">
                    <i class="fas fa-times"></i> ${data.message}
                </div>`;
            }

        } catch (error) {
            console.error('Fetch error:', error);
            alert('Ошибка сети при отправке запроса');
        }
    }
});