using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Extras;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.EpisodeImport;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.MediaFiles.MediaInfo;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles
{
    [TestFixture]
    public class ReviewAdoptionServiceFixture : CoreTest<ReviewAdoptionService>
    {
        private Series _series;
        private string _reviewRoot;
        private string _libraryFile;
        private Episode _episode;
        private ReviewItem _reviewItem;
        private MediaInfoModel _mediaInfo;
        private EpisodeFile _added;

        [SetUp]
        public void Setup()
        {
            _series = new Series
            {
                Id = 4,
                Path = @"C:\tv\Show Name".AsOsAgnostic()
            };

            _reviewRoot = @"C:\review\tv".AsOsAgnostic();
            _libraryFile = LibraryPath("Show Name - S01E01.mp4");
            _episode = new Episode { Id = 11, SeriesId = _series.Id, SeasonNumber = 1, EpisodeNumber = 1 };
            _mediaInfo = new MediaInfoModel { VideoCodecID = "hev1", ContainerFormat = "mp4" };
            _reviewItem = ReviewItemFor(new List<int> { 11 }, "Show Name - S01E01.mkv");
            _added = null;

            Mocker.GetMock<IConfigService>()
                  .Setup(config => config.ReviewLocation)
                  .Returns(_reviewRoot);

            Mocker.GetMock<IConfigService>()
                  .Setup(config => config.KeepOriginalFile)
                  .Returns(false);

            Mocker.GetMock<IKeptEpisodeFileRepository>()
                  .Setup(repository => repository.GetBySeries(_series.Id))
                  .Returns(new List<KeptEpisodeFile>());

            Mocker.GetMock<IReviewItemService>()
                  .Setup(service => service.GetBySeries(_series.Id))
                  .Returns(new List<ReviewItem> { _reviewItem });

            Mocker.GetMock<IDiskProvider>()
                  .Setup(disk => disk.GetFileSize(It.IsAny<string>()))
                  .Returns(4321);

            Mocker.GetMock<IVideoFileInfoReader>()
                  .Setup(reader => reader.GetMediaInfo(It.IsAny<string>()))
                  .Returns(_mediaInfo);

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.Add(It.IsAny<EpisodeFile>()))
                  .Returns<EpisodeFile>(file =>
                  {
                      file.Id = 20;
                      _added = file;
                      return file;
                  });

            GivenDecision(_libraryFile, new List<Episode> { _episode }, new QualityModel(Quality.WEBDL1080p));
        }

        [Test]
        public void should_adopt_a_file_whose_extension_changed_and_keep_grabbed_quality()
        {
            var left = Subject.Adopt(_series, new List<string> { _libraryFile });

            left.Should().BeEmpty();
            _added.Quality.Quality.Should().Be(Quality.Bluray1080p);
            _added.MediaInfo.Should().BeSameAs(_mediaInfo);
            _added.MediaInfo.VideoCodecID.Should().Be("hev1");
            _added.ReleaseGroup.Should().Be("GROUP");
            _added.SceneName.Should().Be("Show.Name.S01E01.BLURAY");
            _added.Languages.Should().ContainSingle().Which.Id.Should().Be(Language.English.Id);
            _added.IndexerFlags.Should().Be(IndexerFlags.Freeleech);
            _added.Size.Should().Be(4321);

            Mocker.GetMock<IReviewItemService>()
                  .Verify(service => service.RemoveForEpisodes(It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 11 }))), Times.Once());
        }

        [Test]
        public void should_adopt_the_same_extension()
        {
            _libraryFile = LibraryPath("Show Name - S01E01.mkv");
            _reviewItem.ReviewPath = Path.Combine(_reviewRoot, "Show Name", "Season 01", "Show Name - S01E01.mkv").AsOsAgnostic();
            GivenDecision(_libraryFile, new List<Episode> { _episode }, new QualityModel(Quality.WEBDL1080p));

            Subject.Adopt(_series, new List<string> { _libraryFile }).Should().BeEmpty();
            _added.Should().NotBeNull();
        }

        [Test]
        public void should_leave_a_file_that_does_not_match_a_review_item()
        {
            var other = LibraryPath("Show Name - S01E02.mp4");

            var left = Subject.Adopt(_series, new List<string> { other });

            left.Should().ContainSingle().Which.Should().Be(other);
            _added.Should().BeNull();
            Mocker.GetMock<IReviewItemService>()
                  .Verify(service => service.RemoveForEpisodes(It.IsAny<IEnumerable<int>>()), Times.Never());
        }

        [Test]
        public void should_adopt_a_multi_episode_file_as_one_episode_file()
        {
            var second = new Episode { Id = 12, SeriesId = _series.Id, SeasonNumber = 1, EpisodeNumber = 2 };
            _libraryFile = LibraryPath("Show Name - S01E01-E02.mp4");
            _reviewItem.EpisodeIds = new List<int> { 11, 12 };
            _reviewItem.ReviewPath = Path.Combine(_reviewRoot, "Show Name", "Season 01", "Show Name - S01E01-E02.mkv").AsOsAgnostic();
            GivenDecision(_libraryFile, new List<Episode> { _episode, second }, new QualityModel(Quality.WEBDL1080p));

            Subject.Adopt(_series, new List<string> { _libraryFile }).Should().BeEmpty();

            _added.Episodes.Value.Select(episode => episode.Id).Should().BeEquivalentTo(new[] { 11, 12 });
            Mocker.GetMock<IMediaFileService>().Verify(service => service.Add(It.IsAny<EpisodeFile>()), Times.Once());
            Mocker.GetMock<IReviewItemService>()
                  .Verify(service => service.RemoveForEpisodes(It.Is<IEnumerable<int>>(ids => ids.OrderBy(id => id).SequenceEqual(new[] { 11, 12 }))), Times.Once());
        }

        [Test]
        public void should_not_adopt_when_parsed_episodes_do_not_match_the_review_item()
        {
            GivenDecision(_libraryFile, new List<Episode> { new Episode { Id = 99 } }, new QualityModel(Quality.WEBDL1080p));

            Subject.Adopt(_series, new List<string> { _libraryFile }).Should().ContainSingle();
            _added.Should().BeNull();
            Mocker.GetMock<IReviewItemService>()
                  .Verify(service => service.RemoveForEpisodes(It.IsAny<IEnumerable<int>>()), Times.Never());
        }

        [Test]
        public void should_adopt_when_the_only_rejection_is_that_it_is_not_an_upgrade()
        {
            GivenDecision(
                _libraryFile,
                new List<Episode> { _episode },
                new QualityModel(Quality.WEBDL1080p),
                new ImportRejection(ImportRejectionReason.NotQualityUpgrade, "Not an upgrade"));

            Subject.Adopt(_series, new List<string> { _libraryFile }).Should().BeEmpty();
            _added.Quality.Quality.Should().Be(Quality.Bluray1080p);
        }

        [Test]
        public void should_not_adopt_a_sample()
        {
            GivenDecision(
                _libraryFile,
                new List<Episode> { _episode },
                new QualityModel(Quality.WEBDL1080p),
                new ImportRejection(ImportRejectionReason.Sample, "Sample"));

            Subject.Adopt(_series, new List<string> { _libraryFile }).Should().ContainSingle();
            _added.Should().BeNull();
        }

        [Test]
        public void should_recycle_the_old_file_when_keep_original_is_off()
        {
            var oldFile = ExistingFile();
            GivenOldFile(oldFile, true);

            Subject.Adopt(_series, new List<string> { _libraryFile });

            var oldPath = Path.Combine(_series.Path, oldFile.RelativePath);
            Mocker.GetMock<IRecycleBinProvider>().Verify(bin => bin.DeleteFile(oldPath, It.IsAny<string>()), Times.Once());
            Mocker.GetMock<IKeptEpisodeFileRepository>().Verify(repository => repository.Insert(It.IsAny<KeptEpisodeFile>()), Times.Never());
            Mocker.GetMock<IMediaFileService>()
                  .Verify(service => service.Delete(It.Is<EpisodeFile>(file => file.Id == oldFile.Id), DeleteMediaFileReason.Upgrade), Times.Once());
        }

        [Test]
        public void should_keep_the_old_file_when_keep_original_is_on()
        {
            Mocker.GetMock<IConfigService>()
                  .Setup(config => config.KeepOriginalFile)
                  .Returns(true);

            var oldFile = ExistingFile();
            GivenOldFile(oldFile, false);

            Subject.Adopt(_series, new List<string> { _libraryFile });

            Mocker.GetMock<IRecycleBinProvider>().Verify(bin => bin.DeleteFile(It.IsAny<string>(), It.IsAny<string>()), Times.Never());
            Mocker.GetMock<IDiskProvider>().Verify(disk => disk.DeleteFile(It.IsAny<string>()), Times.Never());
            Mocker.GetMock<IKeptEpisodeFileRepository>()
                  .Verify(repository => repository.Insert(It.Is<KeptEpisodeFile>(file => file.SeriesId == _series.Id && file.RelativePath == oldFile.RelativePath && file.ReleaseGroup == "OLD")), Times.Once());
            Mocker.GetMock<IMediaFileService>()
                  .Verify(service => service.Delete(It.Is<EpisodeFile>(file => file.Id == oldFile.Id), DeleteMediaFileReason.Upgrade), Times.Once());
        }

        [Test]
        public void should_publish_import_history_and_notifications_once()
        {
            Subject.Adopt(_series, new List<string> { _libraryFile });

            Mocker.GetMock<IEventAggregator>()
                  .Verify(events => events.PublishEvent(It.Is<EpisodeImportedEvent>(message => message.NewDownload && message.DownloadId == "download-1" && message.ImportedEpisode != null && message.EpisodeInfo.Quality.Quality == Quality.Bluray1080p)), Times.Once());
            Mocker.GetMock<IEventAggregator>()
                  .Verify(events => events.PublishEvent(It.Is<UntrackedDownloadCompletedEvent>(message => message.EpisodeFiles.Count == 1 && message.ParsedEpisodeInfo.Quality.Quality == Quality.Bluray1080p)), Times.Once());
            Mocker.GetMock<IExtraService>()
                  .Verify(service => service.ImportEpisode(It.IsAny<LocalEpisode>(), It.Is<EpisodeFile>(file => file.Id == 20), true), Times.Once());
        }

        [Test]
        public void should_not_import_a_kept_original_file()
        {
            var keptPath = LibraryPath("Show Name - S01E03.mkv");
            var keptRelative = _series.Path.GetRelativePath(keptPath);
            var unrelated = LibraryPath("Show Name - S01E04.mkv");

            Mocker.GetMock<IKeptEpisodeFileRepository>()
                  .Setup(repository => repository.GetBySeries(_series.Id))
                  .Returns(new List<KeptEpisodeFile> { new KeptEpisodeFile { SeriesId = _series.Id, RelativePath = keptRelative } });

            var left = Subject.Adopt(_series, new List<string> { keptPath, unrelated, _libraryFile });

            left.Should().BeEquivalentTo(new[] { unrelated });
            _added.Should().NotBeNull();
        }

        private void GivenDecision(string path, List<Episode> episodes, QualityModel parsedQuality, params ImportRejection[] rejections)
        {
            var localEpisode = new LocalEpisode
            {
                Path = path,
                Series = _series,
                Episodes = episodes,
                Quality = parsedQuality,
                FileEpisodeInfo = new ParsedEpisodeInfo { Quality = parsedQuality }
            };

            Mocker.GetMock<IMakeImportDecision>()
                  .Setup(decision => decision.GetImportDecisions(It.Is<List<string>>(files => files.Contains(path)), It.IsAny<Series>(), false))
                  .Returns(new List<ImportDecision> { new ImportDecision(localEpisode, rejections) });
        }

        private void GivenOldFile(EpisodeFile oldFile, bool recycle)
        {
            _episode.EpisodeFileId = oldFile.Id;
            _episode.EpisodeFile = oldFile;

            var root = @"C:\tv".AsOsAgnostic();
            var oldPath = Path.Combine(_series.Path, oldFile.RelativePath);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(disk => disk.GetParentFolder(_series.Path))
                  .Returns(root);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(disk => disk.FolderExists(root))
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(disk => disk.FileExists(oldPath))
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(disk => disk.GetParentFolder(oldPath))
                  .Returns(Path.GetDirectoryName(oldPath));

            if (recycle)
            {
                Mocker.GetMock<IRecycleBinProvider>()
                      .Setup(bin => bin.DeleteFile(oldPath, It.IsAny<string>()))
                      .Returns("recycle");
            }
        }

        private EpisodeFile ExistingFile()
        {
            return new EpisodeFile
            {
                Id = 9,
                SeriesId = _series.Id,
                RelativePath = Path.Combine("Season 01", "old.mkv").AsOsAgnostic(),
                Quality = new QualityModel(Quality.HDTV720p),
                Size = 10,
                ReleaseGroup = "OLD",
                SceneName = "old.scene"
            };
        }

        private ReviewItem ReviewItemFor(List<int> episodeIds, string fileName)
        {
            return new ReviewItem
            {
                Id = 5,
                SeriesId = _series.Id,
                EpisodeIds = episodeIds,
                ReviewPath = Path.Combine(_reviewRoot, "Show Name", "Season 01", fileName).AsOsAgnostic(),
                Quality = new QualityModel(Quality.Bluray1080p),
                Languages = new List<Language> { Language.English },
                ReleaseGroup = "GROUP",
                SceneName = "Show.Name.S01E01.BLURAY",
                IndexerFlags = IndexerFlags.Freeleech,
                ReleaseType = ReleaseType.SingleEpisode,
                DownloadId = "download-1",
                Added = new System.DateTime(2026, 11, 20, 0, 0, 0, System.DateTimeKind.Utc)
            };
        }

        private string LibraryPath(string fileName)
        {
            return Path.Combine(_series.Path, "Season 01", fileName).AsOsAgnostic();
        }
    }
}
