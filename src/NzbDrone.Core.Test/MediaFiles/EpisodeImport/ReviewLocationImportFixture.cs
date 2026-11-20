using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Extras;
using NzbDrone.Core.History;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.EpisodeImport;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles.EpisodeImport
{
    [TestFixture]
    public class ReviewLocationImportFixture : CoreTest<ImportApprovedEpisodes>
    {
        private Series _series;
        private string _downloadPath;
        private string _reviewPath;
        private Episode _episode;
        private EpisodeFile _existingFile;

        [SetUp]
        public void Setup()
        {
            _series = new Series
            {
                Id = 4,
                Path = @"C:\tv\Show Name".AsOsAgnostic(),
                QualityProfile = new QualityProfile { Items = Qualities.QualityFixture.GetDefaultQualities() }
            };

            _downloadPath = @"C:\downloads\Show.Name.S01E01.mkv".AsOsAgnostic();
            _reviewPath = @"C:\review\tv\Show Name\Season 01\Show Name - S01E01.mkv".AsOsAgnostic();

            _existingFile = new EpisodeFile
            {
                Id = 9,
                SeriesId = _series.Id,
                RelativePath = @"Season 01\old.mkv".AsOsAgnostic()
            };

            _episode = new Episode
            {
                Id = 11,
                SeriesId = _series.Id,
                SeasonNumber = 1,
                EpisodeNumber = 1
            };

            Mocker.GetMock<IHistoryService>()
                  .Setup(service => service.FindByDownloadId(It.IsAny<string>()))
                  .Returns(new List<EpisodeHistory>());

            Mocker.GetMock<IDiskProvider>()
                  .Setup(disk => disk.GetFileSize(It.IsAny<string>()))
                  .Returns(1000);

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.GetFilesWithRelativePath(It.IsAny<int>(), It.IsAny<string>()))
                  .Returns(new List<EpisodeFile>());

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.Add(It.IsAny<EpisodeFile>()))
                  .Returns<EpisodeFile>(file => file);

            Mocker.GetMock<IUpgradeMediaFiles>()
                  .Setup(service => service.UpgradeEpisodeFile(It.IsAny<EpisodeFile>(), It.IsAny<LocalEpisode>(), It.IsAny<bool>()))
                  .Returns(new EpisodeFileMoveResult());

            Mocker.GetMock<IMoveEpisodeFiles>()
                  .Setup(service => service.StageEpisodeFile(It.IsAny<EpisodeFile>(), It.IsAny<LocalEpisode>(), It.IsAny<bool>(), It.IsAny<bool>()))
                  .Returns(_reviewPath);
        }

        [Test]
        public void should_stage_a_new_episode_without_creating_an_episode_file()
        {
            GivenReviewEnabled();

            var result = Import(NewEpisode());

            result.Result.Should().Be(ImportResultType.Imported);
            result.StagedForReview.Should().BeTrue();
            result.EpisodeFile.Should().BeNull();
            result.ReviewPath.Should().Be(_reviewPath);

            Mocker.GetMock<IReviewItemService>()
                  .Verify(service => service.Add(It.Is<ReviewItem>(item =>
                      item.SeriesId == _series.Id &&
                      item.EpisodeIds.SequenceEqual(new[] { _episode.Id }) &&
                      item.ReviewPath == _reviewPath &&
                      item.Quality.Quality == Quality.Bluray1080p &&
                      item.ReleaseGroup == "Group" &&
                      item.SceneName == "Show.Name.S01E01" &&
                      item.DownloadId == "download-1" &&
                      item.ReleaseType == ReleaseType.SingleEpisode &&
                      item.IndexerFlags == IndexerFlags.Freeleech &&
                      item.Languages.Contains(Language.English))),
                      Times.Once());

            Mocker.GetMock<IMediaFileService>().Verify(service => service.Add(It.IsAny<EpisodeFile>()), Times.Never());
            Mocker.GetMock<IMediaFileService>().Verify(service => service.Delete(It.IsAny<EpisodeFile>(), It.IsAny<DeleteMediaFileReason>()), Times.Never());
            Mocker.GetMock<IUpgradeMediaFiles>().Verify(service => service.UpgradeEpisodeFile(It.IsAny<EpisodeFile>(), It.IsAny<LocalEpisode>(), It.IsAny<bool>()), Times.Never());
            Mocker.GetMock<IExtraService>().Verify(service => service.ImportEpisode(It.IsAny<LocalEpisode>(), It.IsAny<EpisodeFile>(), It.IsAny<bool>()), Times.Never());
            Mocker.GetMock<IEventAggregator>().Verify(aggregator => aggregator.PublishEvent(It.IsAny<EpisodeImportedEvent>()), Times.Never());
        }

        [Test]
        public void should_stage_an_upgrade_and_leave_the_existing_file_alone()
        {
            GivenReviewEnabled();
            _episode.EpisodeFileId = _existingFile.Id;
            _episode.EpisodeFile = _existingFile;

            var result = Import(NewEpisode());

            result.Result.Should().Be(ImportResultType.Imported);
            result.EpisodeFile.Should().BeNull();
            _episode.EpisodeFileId.Should().Be(_existingFile.Id);

            Mocker.GetMock<IReviewItemService>().Verify(service => service.Add(It.Is<ReviewItem>(item => item.EpisodeIds.Contains(_episode.Id))), Times.Once());
            Mocker.GetMock<IUpgradeMediaFiles>().Verify(service => service.UpgradeEpisodeFile(It.IsAny<EpisodeFile>(), It.IsAny<LocalEpisode>(), It.IsAny<bool>()), Times.Never());
            Mocker.GetMock<IMediaFileService>().Verify(service => service.Add(It.IsAny<EpisodeFile>()), Times.Never());
            Mocker.GetMock<IMediaFileService>().Verify(service => service.Delete(It.IsAny<EpisodeFile>(), It.IsAny<DeleteMediaFileReason>()), Times.Never());
        }

        [Test]
        public void should_store_every_episode_id_from_a_multi_episode_file()
        {
            GivenReviewEnabled();
            var second = new Episode { Id = 12, SeriesId = _series.Id, SeasonNumber = 1, EpisodeNumber = 2 };
            var localEpisode = NewEpisode();
            localEpisode.Episodes = new List<Episode> { _episode, second };

            Import(localEpisode);

            Mocker.GetMock<IReviewItemService>()
                  .Verify(service => service.Add(It.Is<ReviewItem>(item => item.EpisodeIds.SequenceEqual(new[] { 11, 12 }))), Times.Once());
        }

        [Test]
        public void should_import_normally_when_review_location_is_disabled()
        {
            Mocker.GetMock<IConfigService>().SetupGet(config => config.ReviewLocationEnabled).Returns(false);

            var result = Import(NewEpisode());

            result.Result.Should().Be(ImportResultType.Imported);
            result.StagedForReview.Should().BeFalse();
            Mocker.GetMock<IMoveEpisodeFiles>().Verify(service => service.StageEpisodeFile(It.IsAny<EpisodeFile>(), It.IsAny<LocalEpisode>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never());
            Mocker.GetMock<IUpgradeMediaFiles>().Verify(service => service.UpgradeEpisodeFile(It.IsAny<EpisodeFile>(), It.IsAny<LocalEpisode>(), false), Times.Once());
            Mocker.GetMock<IReviewItemService>().Verify(service => service.Add(It.IsAny<ReviewItem>()), Times.Never());
        }

        [Test]
        public void should_not_stage_a_file_that_is_already_in_the_series_folder()
        {
            GivenReviewEnabled();
            var localEpisode = NewEpisode();
            localEpisode.Path = @"C:\tv\Show Name\Season 01\existing.mkv".AsOsAgnostic();

            Subject.Import(new List<ImportDecision> { new ImportDecision(localEpisode) }, false);

            Mocker.GetMock<IMoveEpisodeFiles>().Verify(service => service.StageEpisodeFile(It.IsAny<EpisodeFile>(), It.IsAny<LocalEpisode>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never());
            Mocker.GetMock<IMediaFileService>().Verify(service => service.Add(It.IsAny<EpisodeFile>()), Times.Once());
            Mocker.GetMock<IReviewItemService>().Verify(service => service.Add(It.IsAny<ReviewItem>()), Times.Never());
        }

        [Test]
        public void should_replace_an_existing_review_item_when_the_episode_is_imported_again()
        {
            GivenReviewEnabled();

            var result = Import(NewEpisode());

            result.Result.Should().Be(ImportResultType.Imported);
            Mocker.GetMock<IReviewItemService>().Verify(service => service.RemoveForEpisodes(It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { _episode.Id }))), Times.Once());
            Mocker.GetMock<IMoveEpisodeFiles>().Verify(service => service.StageEpisodeFile(It.IsAny<EpisodeFile>(), It.IsAny<LocalEpisode>(), false, true), Times.Once());
            Mocker.GetMock<IReviewItemService>().Verify(service => service.Add(It.Is<ReviewItem>(item => item.EpisodeIds.SequenceEqual(new[] { _episode.Id }))), Times.Once());
        }

        private void GivenReviewEnabled()
        {
            Mocker.GetMock<IConfigService>().SetupGet(config => config.ReviewLocationEnabled).Returns(true);
        }

        private ImportResult Import(LocalEpisode localEpisode)
        {
            var clientItem = new DownloadClientItem
            {
                DownloadId = "download-1",
                CanMoveFiles = true,
                OutputPath = new OsPath(@"C:\downloads".AsOsAgnostic())
            };

            return Subject.Import(new List<ImportDecision> { new ImportDecision(localEpisode) }, true, clientItem).Single();
        }

        private LocalEpisode NewEpisode()
        {
            return new LocalEpisode
            {
                Series = _series,
                Episodes = new List<Episode> { _episode },
                Path = _downloadPath,
                Quality = new QualityModel(Quality.Bluray1080p),
                ReleaseGroup = "Group",
                SceneName = "Show.Name.S01E01",
                Languages = new List<Language> { Language.English },
                IndexerFlags = IndexerFlags.Freeleech,
                ReleaseType = ReleaseType.SingleEpisode,
                FileEpisodeInfo = new ParsedEpisodeInfo()
            };
        }
    }
}
