using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using MusicStreamingService.Controllers;
using MusicStreamingService.Data;
using MusicStreamingService.Models;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace MusicStreamingService.Tests
{
    [TestFixture]
    public class ControllerTests
    {
        // Вспомогательный метод для создания чистого контекста базы данных в памяти
        private ApplicationDbContext GetInMemoryDbContext(string databaseName = null)
        {
            databaseName ??= $"TestDb_{Guid.NewGuid()}";

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: databaseName)
                .Options;

            var context = new ApplicationDbContext(options);
            context.Database.EnsureCreated();
            return context;
        }

        // Вспомогательный метод для настройки окружения контроллера
        private void MockUserIdentity(Controller controller, string username, string role, int userId)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, role),
                new Claim("UserId", userId.ToString())
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            // Настройка DI контейнера для тестов
            var services = new ServiceCollection();

            // 1. AuthenticationService
            var authServiceMock = new Mock<IAuthenticationService>();
            authServiceMock
                .Setup(_ => _.SignInAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()))
                .Returns(Task.CompletedTask);
            authServiceMock
                .Setup(_ => _.SignOutAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<AuthenticationProperties>()))
                .Returns(Task.CompletedTask);
            services.AddSingleton<IAuthenticationService>(authServiceMock.Object);

            // 2. TempData & Provider
            var tempDataProvider = Mock.Of<ITempDataProvider>();
            services.AddSingleton<ITempDataProvider>(tempDataProvider);

            var tempDataFactoryMock = new Mock<ITempDataDictionaryFactory>();
            tempDataFactoryMock
                .Setup(f => f.GetTempData(It.IsAny<HttpContext>()))
                .Returns((HttpContext c) => new TempDataDictionary(c, tempDataProvider));
            services.AddSingleton<ITempDataDictionaryFactory>(tempDataFactoryMock.Object);

            // 3. UrlHelper
            var urlHelperMock = new Mock<IUrlHelper>();
            urlHelperMock.Setup(x => x.Action(It.IsAny<UrlActionContext>())).Returns("callbackUrl");
            urlHelperMock.Setup(x => x.IsLocalUrl(It.IsAny<string>())).Returns(true);

            var urlHelperFactoryMock = new Mock<IUrlHelperFactory>();
            urlHelperFactoryMock
                .Setup(f => f.GetUrlHelper(It.IsAny<ActionContext>()))
                .Returns(urlHelperMock.Object);
            services.AddSingleton<IUrlHelperFactory>(urlHelperFactoryMock.Object);

            // 4. Logging
            services.AddLogging();

            // Создаем ServiceProvider и HttpContext
            var serviceProvider = services.BuildServiceProvider();
            var httpContext = new DefaultHttpContext();
            httpContext.User = claimsPrincipal;
            httpContext.RequestServices = serviceProvider;

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            controller.TempData = new TempDataDictionary(httpContext, tempDataProvider);
            controller.Url = urlHelperMock.Object;
            controller.ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), controller.ModelState);
        }

        #region HomeController Tests

        [Test]
        public async Task Home_Index_ReturnsViewWithModel()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var loggerMock = new Mock<ILogger<HomeController>>();

            // Добавим данные
            context.Tracks.Add(new Track
            {
                Title = "Track 1",
                IsModerated = true,
                Artist = new Artist { Name = "Artist1" },
                Genre = new Genre { Name = "Genre1" },
                Statistics = new List<TrackStatistics> {
                    new TrackStatistics { ListenCount = 100 }
                }
            });
            context.Albums.Add(new Album
            {
                Title = "Album 1",
                ReleaseDate = DateTime.Now,
                Artist = new Artist { Name = "Artist2" }
            });
            await context.SaveChangesAsync();

            var controller = new HomeController(loggerMock.Object, context);
            MockUserIdentity(controller, "Guest", "Guest", 0);

            // Act
            var result = await controller.Index();

            // Assert
            Assert.IsInstanceOf<ViewResult>(result);
            var viewResult = result as ViewResult;
            Assert.IsInstanceOf<HomeViewModel>(viewResult?.Model);
            var model = viewResult?.Model as HomeViewModel;

            Assert.That(model?.PopularTracks, Is.Not.Null);
            Assert.That(model?.NewReleases, Is.Not.Null);
        }

        [Test]
        public async Task Home_Index_ReturnsEmptyModel_WhenNoData()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var loggerMock = new Mock<ILogger<HomeController>>();

            var controller = new HomeController(loggerMock.Object, context);
            MockUserIdentity(controller, "Guest", "Guest", 0);

            // Act
            var result = await controller.Index();

            // Assert
            Assert.IsInstanceOf<ViewResult>(result);
            var viewResult = result as ViewResult;
            var model = viewResult?.Model as HomeViewModel;

            Assert.That(model?.PopularTracks, Is.Empty);
            Assert.That(model?.NewReleases, Is.Empty);
            Assert.That(model?.FeaturedArtists, Is.Empty);
        }

        #endregion

        #region AccountController Tests

        [Test]
        public async Task Account_Register_Post_ReturnsRedirect_WhenModelIsValid()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var loggerMock = new Mock<ILogger<AccountController>>();
            var controller = new AccountController(context, loggerMock.Object);

            MockUserIdentity(controller, "NewUser", "User", 0);

            var model = new RegisterViewModel
            {
                Username = "NewUser",
                Email = "test@test.com",
                Password = "password123",
                ConfirmPassword = "password123"
            };

            // Act
            var result = await controller.Register(model);

            // Assert
            Assert.IsInstanceOf<RedirectToActionResult>(result);
            var redirectResult = result as RedirectToActionResult;
            Assert.AreEqual("Index", redirectResult?.ActionName);
            Assert.AreEqual("Home", redirectResult?.ControllerName);

            Assert.That(context.Users.Count(), Is.EqualTo(1));
        }

        [Test]
        public async Task Account_Register_Post_ReturnsView_WhenUserExists()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var loggerMock = new Mock<ILogger<AccountController>>();
            var controller = new AccountController(context, loggerMock.Object);

            // Создаем существующего пользователя
            context.Users.Add(new User
            {
                Username = "ExistingUser",
                Email = "existing@test.com",
                PasswordHash = "hash",
            });
            await context.SaveChangesAsync();

            MockUserIdentity(controller, "Guest", "Guest", 0);

            var model = new RegisterViewModel
            {
                Username = "ExistingUser", // Уже существует
                Email = "new@test.com",
                Password = "password123",
                ConfirmPassword = "password123"
            };

            // Act
            var result = await controller.Register(model);

            // Assert
            Assert.IsInstanceOf<ViewResult>(result);
            Assert.IsFalse(controller.ModelState.IsValid);
            Assert.That(controller.ModelState["Username"]?.Errors[0].ErrorMessage,
                Contains.Substring("уже существует"));
        }

        [Test]
        public async Task Account_Login_Post_ReturnsView_WhenUserNotFound()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var loggerMock = new Mock<ILogger<AccountController>>();
            var controller = new AccountController(context, loggerMock.Object);
            MockUserIdentity(controller, "Guest", "Guest", 0);

            var model = new LoginViewModel { UsernameOrEmail = "NonExistent", Password = "123" };

            // Act
            var result = await controller.Login(model);

            // Assert
            Assert.IsInstanceOf<ViewResult>(result);
            Assert.IsFalse(controller.ModelState.IsValid);
        }

        [Test]
        public async Task Account_Login_Post_ReturnsRedirect_WhenCredentialsValid()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var loggerMock = new Mock<ILogger<AccountController>>();
            var controller = new AccountController(context, loggerMock.Object);

            var user = new User
            {
                Username = "TestUser",
                Email = "test@test.com",
                Role = UserRole.User
            };
            user.SetPassword("password123"); // Хешируем пароль
            context.Users.Add(user);
            await context.SaveChangesAsync();

            MockUserIdentity(controller, "Guest", "Guest", 0);

            var model = new LoginViewModel
            {
                UsernameOrEmail = "TestUser",
                Password = "password123"
            };

            // Act
            var result = await controller.Login(model);

            // Assert
            Assert.IsInstanceOf<RedirectToActionResult>(result);
            var redirectResult = result as RedirectToActionResult;
            Assert.AreEqual("Index", redirectResult?.ActionName);
            Assert.AreEqual("Home", redirectResult?.ControllerName);
        }

        #endregion

        #region AdminController Tests

        [Test]
        public async Task Admin_Users_ReturnsViewWithUsers()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var envMock = new Mock<IWebHostEnvironment>();
            envMock.Setup(e => e.WebRootPath).Returns(Path.GetTempPath());

            var controller = new AdminController(context, envMock.Object);
            MockUserIdentity(controller, "Admin", "Admin", 1);

            // Добавляем пользователей
            context.Users.Add(new User { Username = "User1", Email = "u1@test.com", Role = UserRole.User });
            context.Users.Add(new User { Username = "Admin", Email = "a@test.com", Role = UserRole.Admin });
            await context.SaveChangesAsync();

            // Act
            var result = await controller.Users();

            // Assert
            Assert.IsInstanceOf<ViewResult>(result);
            var viewResult = result as ViewResult;
            var model = viewResult?.Model as List<User>;
            Assert.That(model?.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task Admin_Users_WithSearch_ReturnsFilteredUsers()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var envMock = new Mock<IWebHostEnvironment>();
            envMock.Setup(e => e.WebRootPath).Returns(Path.GetTempPath());

            var controller = new AdminController(context, envMock.Object);
            MockUserIdentity(controller, "Admin", "Admin", 1);

            context.Users.Add(new User { Username = "User1", Email = "u1@test.com", Role = UserRole.User });
            context.Users.Add(new User { Username = "Admin", Email = "a@test.com", Role = UserRole.Admin });
            context.Users.Add(new User { Username = "Another", Email = "another@test.com", Role = UserRole.User });
            await context.SaveChangesAsync();

            // Act - поиск по "User"
            var result = await controller.Users("User");

            // Assert
            Assert.IsInstanceOf<ViewResult>(result);
            var viewResult = result as ViewResult;
            var model = viewResult?.Model as List<User>;
            Assert.That(model?.Count, Is.EqualTo(2)); // User1 и Another
        }

        [Test]
        public async Task Admin_CreateGenre_AddsGenreAndRedirects()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var envMock = new Mock<IWebHostEnvironment>();
            envMock.Setup(e => e.WebRootPath).Returns(Path.GetTempPath());

            var controller = new AdminController(context, envMock.Object);
            MockUserIdentity(controller, "Admin", "Admin", 1);

            // Act
            var result = await controller.CreateGenre("Rock");

            // Assert
            Assert.IsInstanceOf<RedirectToActionResult>(result);
            Assert.That(context.Genres.Count(), Is.EqualTo(1));
            Assert.That(context.Genres.First().Name, Is.EqualTo("Rock"));
        }

        [Test]
        public async Task Admin_CreateGenre_WithEmptyName_DoesNotAdd()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var envMock = new Mock<IWebHostEnvironment>();
            envMock.Setup(e => e.WebRootPath).Returns(Path.GetTempPath());

            var controller = new AdminController(context, envMock.Object);
            MockUserIdentity(controller, "Admin", "Admin", 1);

            // Act
            var result = await controller.CreateGenre(""); // Пустое имя

            // Assert
            Assert.IsInstanceOf<RedirectToActionResult>(result);
            Assert.That(context.Genres.Count(), Is.EqualTo(0)); // Не должно добавить
        }

        #endregion

        #region PlaylistsController Tests

        [Test]
        public async Task Playlists_Index_ReturnsUserPlaylistsOnly()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var loggerMock = new Mock<ILogger<PlaylistsController>>();

            var user1 = new User { Id = 1, Username = "User1", Email = "u1@t.com", Role = UserRole.User };
            var user2 = new User { Id = 2, Username = "User2", Email = "u2@t.com", Role = UserRole.User };
            context.Users.AddRange(user1, user2);
            await context.SaveChangesAsync();

            context.Playlists.Add(new Playlist { Title = "P1", UserId = 1, User = user1 });
            context.Playlists.Add(new Playlist { Title = "P2", UserId = 2, User = user2 });
            await context.SaveChangesAsync();

            var controller = new PlaylistsController(context, loggerMock.Object);
            MockUserIdentity(controller, "User1", "User", 1);

            // Act
            var result = await controller.Index();

            // Assert
            Assert.IsInstanceOf<ViewResult>(result);
            var model = (result as ViewResult)?.Model as List<Playlist>;

            Assert.That(model?.Count, Is.EqualTo(1));
            Assert.That(model?.First().Title, Is.EqualTo("P1"));
        }

        [Test]
        public async Task Playlists_Create_Post_AddsPlaylist_WhenUserCanCreate()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var loggerMock = new Mock<ILogger<PlaylistsController>>();

            var user = new User { Id = 1, Username = "SubUser", Email = "sub@t.com", Role = UserRole.Subscriber };
            user.Subscriptions.Add(new Subscription
            {
                IsActivated = true,
                EndDate = DateTime.Now.AddDays(30)
            });
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var controller = new PlaylistsController(context, loggerMock.Object);
            MockUserIdentity(controller, "SubUser", "Subscriber", 1);

            var model = new PlaylistCreateViewModel
            {
                Title = "My Mix",
                Description = "Cool songs",
                IsPublic = true
            };

            // Act
            var result = await controller.Create(model);

            // Assert
            Assert.IsInstanceOf<RedirectToActionResult>(result);
            Assert.That(context.Playlists.Count(), Is.EqualTo(1));
            Assert.That(context.Playlists.First().Title, Is.EqualTo("My Mix"));
            Assert.That(context.Playlists.First().Visibility, Is.EqualTo(PlaylistVisibility.Public));
        }

        [Test]
        public async Task Playlists_Create_ReturnsRedirect_WhenUserCannotCreate()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var loggerMock = new Mock<ILogger<PlaylistsController>>();

            var user = new User { Id = 1, Username = "RegularUser", Role = UserRole.User };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var controller = new PlaylistsController(context, loggerMock.Object);
            MockUserIdentity(controller, "RegularUser", "User", 1);

            // Act - попытка создать без подписки
            var result = await controller.Create(new PlaylistCreateViewModel());

            // Assert - должен быть редирект с сообщением об ошибке
            Assert.IsInstanceOf<RedirectToActionResult>(result);
            var redirectResult = result as RedirectToActionResult;
            Assert.AreEqual("Profile", redirectResult?.ActionName);
            Assert.AreEqual("Account", redirectResult?.ControllerName);
        }

        #endregion

        #region SearchController Tests

        [Test]
        public async Task Search_Index_ReturnsResults()
        {
            // Arrange
            using var context = GetInMemoryDbContext();

            var genre = new Genre { Name = "Rock" };
            context.Genres.Add(genre);

            var artist = new Artist { Name = "Metallica" };
            context.Artists.Add(artist);

            await context.SaveChangesAsync();

            var track = new Track
            {
                Title = "Enter Sandman",
                ArtistId = artist.Id,
                Artist = artist,
                GenreId = genre.Id,
                Genre = genre,
                IsModerated = true,
                Duration = 300
            };
            context.Tracks.Add(track);
            await context.SaveChangesAsync();

            var controller = new SearchController(context);
            MockUserIdentity(controller, "Guest", "User", 0);

            // Act
            var result = await controller.Index("Metallica");

            // Assert
            Assert.IsInstanceOf<ViewResult>(result);
            var model = (result as ViewResult)?.Model as SearchViewModel;

            Assert.That(model?.Query, Is.EqualTo("Metallica"));
            Assert.That(model?.Tracks.Count, Is.EqualTo(1));
            Assert.That(model?.Artists.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task Search_Index_WithEmptyQuery_ReturnsRedirect()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var controller = new SearchController(context);
            MockUserIdentity(controller, "Guest", "User", 0);

            // Act
            var result = await controller.Index("");

            // Assert
            Assert.IsInstanceOf<RedirectToActionResult>(result);
            var redirectResult = result as RedirectToActionResult;
            Assert.AreEqual("Index", redirectResult?.ActionName);
            Assert.AreEqual("Home", redirectResult?.ControllerName);
        }

        [Test]
        public async Task Search_Index_OnlyModeratedTracks()
        {
            // Arrange
            using var context = GetInMemoryDbContext();

            // Создаем все необходимые сущности
            var artist = new Artist { Id = 1, Name = "TestArtist" };
            var genre = new Genre { Id = 1, Name = "Pop" };
            var album = new Album { Id = 1, Title = "Test Album", ArtistId = 1 };

            context.Artists.Add(artist);
            context.Genres.Add(genre);
            context.Albums.Add(album);
            await context.SaveChangesAsync();

            // Модерированный трек
            var moderatedTrack = new Track
            {
                Title = "Moderated Song",
                ArtistId = artist.Id,
                Artist = artist,
                GenreId = genre.Id,
                Genre = genre,
                AlbumId = album.Id,
                Album = album,
                IsModerated = true,
                Duration = 180,
                Statistics = new List<TrackStatistics>()
            };
            context.Tracks.Add(moderatedTrack);

            // Немодерированный трек
            var unmoderatedTrack = new Track
            {
                Title = "Unmoderated Song",
                ArtistId = artist.Id,
                Artist = artist,
                GenreId = genre.Id,
                Genre = genre,
                AlbumId = album.Id,
                Album = album,
                IsModerated = false,
                Duration = 200,
                Statistics = new List<TrackStatistics>()
            };
            context.Tracks.Add(unmoderatedTrack);

            await context.SaveChangesAsync();

            var controller = new SearchController(context);
            MockUserIdentity(controller, "Guest", "User", 0);

            // Act - ищем по части названия
            var result = await controller.Index("Song");

            // Assert
            Assert.IsInstanceOf<ViewResult>(result);
            var model = (result as ViewResult)?.Model as SearchViewModel;

            Assert.That(model, Is.Not.Null);
            Assert.That(model?.Tracks.Count, Is.EqualTo(1),
                "Должен найти только 1 модерированный трек");
            Assert.That(model?.Tracks.First().Title, Is.EqualTo("Moderated Song"));
            Assert.That(model?.Tracks.First().IsModerated, Is.True);
        }

        #endregion

        #region ModerationController Tests

        [Test]
        public async Task Moderation_Approve_UpdatesTrackStatus()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var admin = new User { Id = 10, Username = "Admin", Role = UserRole.Admin };
            context.Users.Add(admin);

            var track = new Track { Id = 1, Title = "Demo", IsModerated = false };
            context.Tracks.Add(track);
            await context.SaveChangesAsync();

            var controller = new ModerationController(context);
            MockUserIdentity(controller, "Admin", "Admin", 10);

            // Act
            var result = await controller.Approve(1, "Good track");

            // Assert
            Assert.IsInstanceOf<RedirectToActionResult>(result);

            var updatedTrack = await context.Tracks.FindAsync(1);
            Assert.IsTrue(updatedTrack.IsModerated);

            var moderation = await context.Moderations.FirstOrDefaultAsync(m => m.TrackId == 1);
            Assert.That(moderation, Is.Not.Null);
            Assert.That(moderation.Status, Is.EqualTo(ModerationStatus.Approved));
        }

        [Test]
        public async Task Moderation_Reject_AddsModerationRecord()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var admin = new User { Id = 11, Username = "Admin", Role = UserRole.Admin };
            context.Users.Add(admin);

            var track = new Track { Id = 2, Title = "Bad Track", IsModerated = false };
            context.Tracks.Add(track);
            await context.SaveChangesAsync();

            var controller = new ModerationController(context);
            MockUserIdentity(controller, "Admin", "Admin", 11);

            // Act
            var result = await controller.Reject(2, "Poor quality");

            // Assert
            Assert.IsInstanceOf<RedirectToActionResult>(result);

            var moderation = await context.Moderations.FirstOrDefaultAsync(m => m.TrackId == 2);
            Assert.That(moderation, Is.Not.Null);
            Assert.That(moderation.Status, Is.EqualTo(ModerationStatus.Rejected));
            Assert.That(moderation.Comment, Contains.Substring("Poor quality"));
        }

        [Test]
        public async Task Moderation_Index_ShowsOnlyPendingTracks()
        {
            // Arrange
            using var context = GetInMemoryDbContext();

            var artist = new Artist { Name = "Test Artist" };
            var genre = new Genre { Name = "Test Genre" };
            context.Artists.Add(artist);
            context.Genres.Add(genre);
            await context.SaveChangesAsync();

            // Pending track - без модерации
            context.Tracks.Add(new Track
            {
                Id = 1,
                Title = "Pending",
                IsModerated = false,
                ArtistId = artist.Id,
                GenreId = genre.Id
            });

            // Approved track - уже модерирован
            context.Tracks.Add(new Track
            {
                Id = 2,
                Title = "Approved",
                IsModerated = true,
                ArtistId = artist.Id,
                GenreId = genre.Id
            });

            // Track with rejection record - есть запись об отклонении
            var rejectedTrack = new Track
            {
                Id = 3,
                Title = "Rejected",
                IsModerated = false,
                ArtistId = artist.Id,
                GenreId = genre.Id
            };
            context.Tracks.Add(rejectedTrack);

            await context.SaveChangesAsync();

            // Добавляем запись модерации для отклоненного трека
            var adminUser = new User
            {
                Id = 100,
                Username = "Admin",
                Role = UserRole.Admin
            };
            context.Users.Add(adminUser);

            context.Moderations.Add(new Moderation
            {
                TrackId = 3,
                Status = ModerationStatus.Rejected,
                ModeratorId = 100,
                ModerationDate = DateTime.UtcNow,
                Comment = "Rejected in test"
            });

            await context.SaveChangesAsync();

            var controller = new ModerationController(context);
            MockUserIdentity(controller, "Admin", "Admin", 100);

            // Act
            var result = await controller.Index();

            // Assert - должен показать только трек 1 (pending без записей модерации)
            Assert.IsInstanceOf<ViewResult>(result);
            var model = (result as ViewResult)?.Model as List<Track>;
            Assert.That(model?.Count, Is.EqualTo(1));
            Assert.That(model?.First().Title, Is.EqualTo("Pending"));
            Assert.That(model?.First().Id, Is.EqualTo(1));
        }

        #endregion

        #region SubscriptionController Tests

        [Test]
        public async Task Subscription_Create_Post_AddsSubscriptionAndUpdatesRole()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var loggerMock = new Mock<ILogger<SubscriptionController>>();

            var configMock = new Mock<IConfiguration>();
            var configSectionMock = new Mock<IConfigurationSection>();
            configSectionMock.Setup(x => x.Value).Returns("true");
            configMock.Setup(c => c.GetSection("Sberbank:TestMode")).Returns(configSectionMock.Object);

            var user = new User { Id = 5, Username = "UserToSub", Role = UserRole.User };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var controller = new SubscriptionController(context, loggerMock.Object, configMock.Object);
            MockUserIdentity(controller, "UserToSub", "User", 5);

            var model = new SubscriptionViewModel
            {
                CardName = "TEST",
                CardNumber = "2200000000000004",
                ExpiryDate = "12/25",
                CVV = "123",
                AgreeToTerms = true
            };

            // Act
            var result = await controller.Create(model);

            // Assert
            Assert.IsInstanceOf<RedirectToActionResult>(result);
            var redirectResult = result as RedirectToActionResult;
            Assert.AreEqual("Success", redirectResult?.ActionName);

            var updatedUser = await context.Users.FindAsync(5);
            Assert.That(updatedUser.Role, Is.EqualTo(UserRole.Subscriber));
            Assert.That(context.Subscriptions.Count(), Is.EqualTo(1));
        }

        [Test]
        public async Task Subscription_Create_ReturnsView_WhenModelInvalid()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var loggerMock = new Mock<ILogger<SubscriptionController>>();
            var configMock = new Mock<IConfiguration>();

            var controller = new SubscriptionController(context, loggerMock.Object, configMock.Object);
            MockUserIdentity(controller, "User", "User", 1);

            // Создаем пользователя в базе
            var user = new User
            {
                Id = 1,
                Username = "User",
                Email = "user@test.com",
                Role = UserRole.User
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            // Создаем невалидную модель - не согласен с условиями
            var model = new SubscriptionViewModel
            {
                CardName = "", // Пустое имя
                CardNumber = "", // Пустой номер карты
                ExpiryDate = "", // Пустая дата
                CVV = "", // Пустой CVV
                AgreeToTerms = false // Не согласен с условиями - это критично!
            };

            // Вручную добавляем ошибки в ModelState
            controller.ModelState.AddModelError("CardName", "Имя на карте обязательно");
            controller.ModelState.AddModelError("CardNumber", "Номер карты обязателен");
            controller.ModelState.AddModelError("ExpiryDate", "Дата окончания обязательна");
            controller.ModelState.AddModelError("CVV", "CVV обязателен");
            controller.ModelState.AddModelError("AgreeToTerms", "Необходимо согласие с условиями");

            // Act
            var result = await controller.Create(model);

            // Assert
            Assert.IsInstanceOf<ViewResult>(result);
            Assert.IsFalse(controller.ModelState.IsValid);

            // Проверяем, что подписка НЕ создана
            Assert.That(context.Subscriptions.Count(), Is.EqualTo(0));
        }

        [Test]
        public async Task Subscription_My_ReturnsUserSubscriptions()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var loggerMock = new Mock<ILogger<SubscriptionController>>();
            var configMock = new Mock<IConfiguration>();

            var user = new User
            {
                Id = 6,
                Username = "Subscriber",
                Email = "sub@test.com",
                Role = UserRole.Subscriber
            };

            // Активная подписка
            user.Subscriptions.Add(new Subscription
            {
                StartDate = DateTime.Now.AddDays(-10),
                EndDate = DateTime.Now.AddDays(20),
                IsActivated = true,
                Amount = 299
            });

            // Истекшая подписка
            user.Subscriptions.Add(new Subscription
            {
                StartDate = DateTime.Now.AddDays(-40),
                EndDate = DateTime.Now.AddDays(-10),
                IsActivated = false,
                Amount = 299
            });

            context.Users.Add(user);
            await context.SaveChangesAsync();

            var controller = new SubscriptionController(context, loggerMock.Object, configMock.Object);
            MockUserIdentity(controller, "Subscriber", "Subscriber", 6);

            // Act
            var result = await controller.My();

            // Assert
            Assert.IsInstanceOf<ViewResult>(result);
            var model = (result as ViewResult)?.Model as List<Subscription>;
            Assert.That(model?.Count, Is.EqualTo(2));
        }

        #endregion

        #region StatisticsController Tests

        [Test]
        public async Task Statistics_Tracks_ReturnsStats()
        {
            // Arrange
            using var context = GetInMemoryDbContext();

            var artist = new Artist { Name = "Star" };
            context.Artists.Add(artist);
            await context.SaveChangesAsync();

            var track = new Track
            {
                Id = 1,
                Title = "Hit Song",
                ArtistId = artist.Id,
                Artist = artist
            };
            context.Tracks.Add(track);

            context.TrackStatistics.Add(new TrackStatistics
            {
                TrackId = 1,
                ListenCount = 50,
                Date = DateTime.UtcNow
            });
            context.TrackStatistics.Add(new TrackStatistics
            {
                TrackId = 1,
                ListenCount = 10,
                Date = DateTime.UtcNow.AddDays(-1)
            });
            await context.SaveChangesAsync();

            var controller = new StatisticsController(context);
            MockUserIdentity(controller, "Admin", "Admin", 1);

            // Act
            var result = await controller.Tracks();

            // Assert
            Assert.IsInstanceOf<ViewResult>(result);
            var model = (result as ViewResult)?.Model as List<TrackStatViewModel>;

            Assert.That(model?.Count, Is.EqualTo(1));
            Assert.That(model?.First().TotalListens, Is.EqualTo(60));
        }

        [Test]
        public async Task Statistics_Tracks_WithPeriodFilter_ReturnsFilteredStats()
        {
            // Arrange
            using var context = GetInMemoryDbContext();

            var artist = new Artist { Name = "Artist" };
            context.Artists.Add(artist);
            await context.SaveChangesAsync();

            var track = new Track
            {
                Id = 1,
                Title = "Track",
                ArtistId = artist.Id,
                Artist = artist
            };
            context.Tracks.Add(track);

            // Статистика за сегодня
            context.TrackStatistics.Add(new TrackStatistics
            {
                TrackId = 1,
                ListenCount = 10,
                Date = DateTime.UtcNow
            });

            // Статистика за старую дату
            context.TrackStatistics.Add(new TrackStatistics
            {
                TrackId = 1,
                ListenCount = 100,
                Date = DateTime.UtcNow.AddDays(-100)
            });

            await context.SaveChangesAsync();

            var controller = new StatisticsController(context);
            MockUserIdentity(controller, "Admin", "Admin", 1);

            // Act - фильтр "today"
            var result = await controller.Tracks(period: "today");

            // Assert - только сегодняшняя статистика
            var model = (result as ViewResult)?.Model as List<TrackStatViewModel>;
            Assert.That(model?.Count, Is.EqualTo(1));
            Assert.That(model?.First().TotalListens, Is.EqualTo(10));
        }

        [Test]
        public async Task Statistics_Albums_ReturnsAlbumStats()
        {
            // Arrange
            using var context = GetInMemoryDbContext();

            var artist = new Artist { Name = "Album Artist" };
            context.Artists.Add(artist);

            var album = new Album
            {
                Id = 1,
                Title = "Great Album",
                ArtistId = artist.Id,
                Artist = artist
            };
            context.Albums.Add(album);

            var track = new Track
            {
                Id = 1,
                Title = "Album Track",
                ArtistId = artist.Id,
                AlbumId = album.Id,
                Album = album,
                IsModerated = true
            };
            context.Tracks.Add(track);

            context.TrackStatistics.Add(new TrackStatistics
            {
                TrackId = 1,
                ListenCount = 25,
                Date = DateTime.UtcNow
            });

            await context.SaveChangesAsync();

            var controller = new StatisticsController(context);
            MockUserIdentity(controller, "Admin", "Admin", 1);

            // Act
            var result = await controller.Albums();

            // Assert
            Assert.IsInstanceOf<ViewResult>(result);
            var model = (result as ViewResult)?.Model as List<AlbumStatViewModel>;

            Assert.That(model?.Count, Is.EqualTo(1));
            Assert.That(model?.First().AlbumTitle, Is.EqualTo("Great Album"));
            Assert.That(model?.First().TotalListens, Is.EqualTo(25));
        }

        #endregion

        #region TracksController Tests

        [Test]
        public async Task Tracks_Create_Post_SavesTrack_WhenUserIsMusician()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var envMock = new Mock<IWebHostEnvironment>();
            var loggerMock = new Mock<ILogger<TracksController>>();

            // Создаем временную директорию для сохранения файла
            var tempDir = Path.Combine(Path.GetTempPath(), $"TestAudio_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            // Важно: WebRootPath должен указывать на корень с папкой audio
            var wwwrootPath = Path.Combine(tempDir, "wwwroot");
            var audioPath = Path.Combine(wwwrootPath, "audio");
            Directory.CreateDirectory(audioPath);

            envMock.Setup(e => e.WebRootPath).Returns(wwwrootPath);

            var musician = new User
            {
                Id = 99,
                Username = "Musician",
                Role = UserRole.Musician
            };
            context.Users.Add(musician);

            context.Genres.Add(new Genre { Id = 1, Name = "Pop" });
            context.Artists.Add(new Artist { Id = 1, Name = "Self" });

            // Добавляем администратора для модерации
            var admin = new User
            {
                Id = 100,
                Username = "Admin",
                Role = UserRole.Admin
            };
            context.Users.Add(admin);

            await context.SaveChangesAsync();

            var controller = new TracksController(context, envMock.Object, loggerMock.Object);
            MockUserIdentity(controller, "Musician", "Musician", 99);

            // Создаем fake файл в памяти
            var fileContent = "fake audio content";
            var fileName = "song.mp3";
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(fileContent);
            writer.Flush();
            stream.Position = 0;

            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.FileName).Returns(fileName);
            fileMock.Setup(f => f.Length).Returns(stream.Length);
            fileMock.Setup(f => f.OpenReadStream()).Returns(stream);
            fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                   .Returns((Stream target, CancellationToken token) =>
                   {
                       stream.Position = 0;
                       return stream.CopyToAsync(target, 81920, token);
                   });

            var model = new TrackCreateViewModel
            {
                Title = "New Song",
                GenreId = 1,
                ArtistId = 1,
                Duration = 180,
                AudioFile = fileMock.Object
            };

            try
            {
                // Act
                var result = await controller.Create(model);

                // Assert
                Assert.IsInstanceOf<RedirectToActionResult>(result);
                var redirectResult = result as RedirectToActionResult;
                Assert.AreEqual("Profile", redirectResult?.ActionName);
                Assert.AreEqual("Account", redirectResult?.ControllerName);

                // Проверяем что трек сохранен
                Assert.That(context.Tracks.Count(), Is.EqualTo(1));
                var savedTrack = await context.Tracks.FirstOrDefaultAsync();
                Assert.That(savedTrack, Is.Not.Null);
                Assert.That(savedTrack.UploadedByUserId, Is.EqualTo(99));
                Assert.That(savedTrack.IsModerated, Is.False);
                Assert.That(savedTrack.Title, Is.EqualTo("New Song"));

                // Проверяем что запись модерации создана
                var moderation = await context.Moderations.FirstOrDefaultAsync();
                Assert.That(moderation, Is.Not.Null);
                Assert.That(moderation.TrackId, Is.EqualTo(savedTrack.Id));
                Assert.That(moderation.ModeratorId, Is.EqualTo(100)); // ID администратора
            }
            finally
            {
                // Очистка
                if (Directory.Exists(tempDir))
                {
                    try
                    {
                        Directory.Delete(tempDir, true);
                    }
                    catch
                    {
                        // Игнорируем ошибки очистки
                    }
                }
            }
        }

        [Test]
        public async Task Tracks_Create_ReturnsRedirect_WhenUserCannotUpload()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var envMock = new Mock<IWebHostEnvironment>();
            var loggerMock = new Mock<ILogger<TracksController>>();

            var regularUser = new User
            {
                Id = 100,
                Username = "Regular",
                Role = UserRole.User
            };
            context.Users.Add(regularUser);
            await context.SaveChangesAsync();

            var controller = new TracksController(context, envMock.Object, loggerMock.Object);
            MockUserIdentity(controller, "Regular", "User", 100);

            // Act - GET запрос
            var result = controller.Create();

            // Assert - должен быть редирект
            Assert.IsInstanceOf<RedirectToActionResult>(result.Result);
            var redirectResult = result.Result as RedirectToActionResult;
            Assert.AreEqual("Profile", redirectResult?.ActionName);
            Assert.AreEqual("Account", redirectResult?.ControllerName);
        }

        [Test]
        public async Task Tracks_Play_ReturnsView_WithTrackDetails()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var envMock = new Mock<IWebHostEnvironment>();
            var loggerMock = new Mock<ILogger<TracksController>>();

            var artist = new Artist { Name = "Test Artist" };
            var genre = new Genre { Name = "Test Genre" };
            var album = new Album { Title = "Test Album", Artist = artist };

            context.Artists.Add(artist);
            context.Genres.Add(genre);
            context.Albums.Add(album);
            await context.SaveChangesAsync();

            var track = new Track
            {
                Title = "Test Track",
                ArtistId = artist.Id,
                Artist = artist,
                GenreId = genre.Id,
                Genre = genre,
                AlbumId = album.Id,
                Album = album,
                IsModerated = true,
                Duration = 200
            };
            context.Tracks.Add(track);
            await context.SaveChangesAsync();

            var controller = new TracksController(context, envMock.Object, loggerMock.Object);
            MockUserIdentity(controller, "User", "User", 1);

            // Act
            var result = await controller.Play(track.Id);

            // Assert
            Assert.IsInstanceOf<ViewResult>(result);
            var viewResult = result as ViewResult;
            var model = viewResult?.Model as Track;

            Assert.That(model?.Title, Is.EqualTo("Test Track"));
            Assert.That(model?.Artist?.Name, Is.EqualTo("Test Artist"));

            // Проверяем, что добавилась статистика
            var stats = await context.TrackStatistics.CountAsync();
            Assert.That(stats, Is.EqualTo(1));
        }

        [Test]
        public async Task Tracks_Play_ReturnsNotFound_ForUnmoderatedTrack()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var envMock = new Mock<IWebHostEnvironment>();
            var loggerMock = new Mock<ILogger<TracksController>>();

            var track = new Track
            {
                Id = 999,
                Title = "Unmoderated",
                IsModerated = false
            };
            context.Tracks.Add(track);
            await context.SaveChangesAsync();

            var controller = new TracksController(context, envMock.Object, loggerMock.Object);
            MockUserIdentity(controller, "User", "User", 1);

            // Act
            var result = await controller.Play(999);

            // Assert
            Assert.IsInstanceOf<NotFoundResult>(result);
        }

        #endregion

        #region Граничные значения - TracksController Edge Cases

        [TestFixture]
        public class TracksControllerEdgeCaseTests
        {
            private ApplicationDbContext _context;
            private TracksController _controller;
            private Mock<IWebHostEnvironment> _envMock;
            private Mock<ILogger<TracksController>> _loggerMock;

            [SetUp]
            public void Setup()
            {
                _context = GetInMemoryDbContext();
                _envMock = new Mock<IWebHostEnvironment>();
                _loggerMock = new Mock<ILogger<TracksController>>();
                _envMock.Setup(e => e.WebRootPath).Returns(Path.GetTempPath());
                _controller = new TracksController(_context, _envMock.Object, _loggerMock.Object);
            }

            [TearDown]
            public void TearDown()
            {
                _context?.Dispose();
                if (_controller is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            private ApplicationDbContext GetInMemoryDbContext()
            {
                var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase(databaseName: $"EdgeCaseDb_{Guid.NewGuid()}")
                    .Options;

                return new ApplicationDbContext(options);
            }

            [Test]
            public async Task RecordPlay_ReturnsOk_ForExistingModeratedTrack()
            {
                // Arrange
                var track = new Track
                {
                    Id = 1,
                    Title = "Test Track",
                    IsModerated = true
                };
                _context.Tracks.Add(track);
                await _context.SaveChangesAsync();

                var initialCount = await _context.TrackStatistics.CountAsync();

                // Act
                var result = await _controller.RecordPlay(1);

                // Assert
                Assert.IsInstanceOf<OkResult>(result);

                var finalCount = await _context.TrackStatistics.CountAsync();
                var newStat = await _context.TrackStatistics
                    .FirstOrDefaultAsync(ts => ts.TrackId == 1);

                Assert.That(finalCount, Is.EqualTo(initialCount + 1));
                Assert.That(newStat, Is.Not.Null);
                Assert.That(newStat.ListenCount, Is.EqualTo(1));
            }

            [Test]
            public async Task RecordPlay_ReturnsNotFound_ForNonExistingTrack()
            {
                // Arrange - трек не существует
                // Act
                var result = await _controller.RecordPlay(999);

                // Assert
                Assert.IsInstanceOf<NotFoundResult>(result);
            }

            [Test]
            public async Task RecordPlay_ReturnsNotFound_ForUnmoderatedTrack()
            {
                // Arrange - трек существует, но не модерирован
                var track = new Track
                {
                    Id = 2,
                    Title = "Unmoderated Track",
                    IsModerated = false
                };
                _context.Tracks.Add(track);
                await _context.SaveChangesAsync();

                // Act
                var result = await _controller.RecordPlay(2);

                // Assert
                Assert.IsInstanceOf<NotFoundResult>(result);

                // Проверяем, что статистика НЕ записана
                var statExists = await _context.TrackStatistics
                    .AnyAsync(ts => ts.TrackId == 2);
                Assert.IsFalse(statExists);
            }

            [Test]
            public async Task RecordPlay_ReturnsOk_ForTrackIdMaxValue()
            {
                // Arrange - ID с максимальным значением
                var maxId = int.MaxValue;
                var track = new Track
                {
                    Id = maxId,
                    Title = "Track with Max ID",
                    IsModerated = true
                };
                _context.Tracks.Add(track);
                await _context.SaveChangesAsync();

                // Act
                var result = await _controller.RecordPlay(maxId);

                // Assert
                Assert.IsInstanceOf<OkResult>(result);

                var newStat = await _context.TrackStatistics
                    .FirstOrDefaultAsync(ts => ts.TrackId == maxId);
                Assert.That(newStat, Is.Not.Null);
            }

            [Test]
            public async Task RecordPlay_MultipleCalls_IncrementsStatistics()
            {
                // Arrange - многократный вызов
                var track = new Track
                {
                    Id = 5,
                    Title = "Popular Track",
                    IsModerated = true
                };
                _context.Tracks.Add(track);
                await _context.SaveChangesAsync();

                // Act - вызываем 3 раза
                await _controller.RecordPlay(5);
                await _controller.RecordPlay(5);
                await _controller.RecordPlay(5);

                // Assert
                var stats = await _context.TrackStatistics
                    .Where(ts => ts.TrackId == 5)
                    .ToListAsync();

                Assert.That(stats.Count, Is.EqualTo(3));
                Assert.That(stats.Sum(s => s.ListenCount), Is.EqualTo(3));
            }

            [Test]
            public async Task RecordPlay_WithConcurrentCalls_HandlesGracefully()
            {
                // Arrange
                var track = new Track
                {
                    Id = 10,
                    Title = "Concurrent Test Track",
                    IsModerated = true
                };
                _context.Tracks.Add(track);
                await _context.SaveChangesAsync();

                // Act - несколько "параллельных" вызовов
                var tasks = Enumerable.Range(0, 5)
                    .Select(_ => _controller.RecordPlay(10))
                    .ToList();

                await Task.WhenAll(tasks);

                // Assert - все вызовы завершились успешно
                foreach (var task in tasks)
                {
                    Assert.IsInstanceOf<OkResult>(await task);
                }

                var totalListens = await _context.TrackStatistics
                    .Where(ts => ts.TrackId == 10)
                    .SumAsync(ts => ts.ListenCount);

                Assert.That(totalListens, Is.EqualTo(5));
            }

            [Test]
            public async Task RecordPlay_NegativeTrackId_ReturnsNotFound()
            {
                // Arrange - отрицательный ID
                // Act
                var result = await _controller.RecordPlay(-1);

                // Assert
                Assert.IsInstanceOf<NotFoundResult>(result);
            }

            [Test]
            public async Task RecordPlay_EmptyDatabase_ReturnsNotFound()
            {
                // Arrange - база данных пустая
                // Act
                var result = await _controller.RecordPlay(1);

                // Assert
                Assert.IsInstanceOf<NotFoundResult>(result);
            }

            [Test]
            public async Task RecordPlay_TrackIdWithSpecialCharactersInTitle_Works()
            {
                // Arrange - трек с "специальными" символами в названии
                var track = new Track
                {
                    Id = 30,
                    Title = "Track @#$%^&*() Test",
                    IsModerated = true
                };
                _context.Tracks.Add(track);
                await _context.SaveChangesAsync();

                // Act
                var result = await _controller.RecordPlay(30);

                // Assert
                Assert.IsInstanceOf<OkResult>(result);
            }
        }

        #endregion
    }
}
