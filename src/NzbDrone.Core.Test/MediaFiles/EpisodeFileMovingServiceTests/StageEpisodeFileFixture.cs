using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.RootFolders;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles.EpisodeFileMovingServiceTests
{
    [TestFixture]
    public class StageEpisodeFileFixture : CoreTest<EpisodeFileMovingService>
    {
        private Series _series;
        private string _seriesPath;
        private string _reviewRoot;
        private string _source;
        private LocalEpisode _localEpisode;
        private EpisodeFile _episodeFile;

        [SetUp]
        public void Setup()
        {
            _seriesPath = @"C:\tv\Show Name".AsOsAgnostic();
            _reviewRoot = @"C:\review\tv".AsOsAgnostic();
            _source = @"C:\downloads\Show.Name.S01E01.mkv".AsOsAgnostic();

            _series = new Series
            {
                Id = 4,
                Path = _seriesPath,
                SeasonFolder = true
            };

            _episodeFile = new EpisodeFile
            {
                SeriesId = _series.Id,
                Path = _source
            };

            _localEpisode = new LocalEpisode
            {
                Series = _series,
                Episodes = new List<Episode> { new Episode { Id = 11, SeasonNumber = 1, EpisodeNumber = 1 } },
                Path = _source,
                CustomFormats = new List<CustomFormat>()
            };

            Mocker.GetMock<IConfigService>()
                  .SetupGet(config => config.ReviewLocation)
                  .Returns(_reviewRoot);

            Mocker.GetMock<IConfigService>()
                  .SetupGet(config => config.CopyUsingHardlinks)
                  .Returns(false);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(disk => disk.FolderExists(It.IsAny<string>()))
                  .Returns<string>(path => path.PathEquals(_reviewRoot));

            Mocker.GetMock<IDiskProvider>()
                  .Setup(disk => disk.FileExists(It.IsAny<string>()))
                  .Returns<string>(path => path.PathEquals(_source));
        }

        [Test]
        public void should_place_the_file_under_the_existing_series_folder_name()
        {
            var libraryPath = Path.Combine(_seriesPath, "Season 01", "Show Name - S01E01.mkv");
            var expected = Path.Combine(_reviewRoot, "Show Name", "Season 01", "Show Name - S01E01.mkv");
            GivenLibraryPath(libraryPath);

            var result = Subject.StageEpisodeFile(_episodeFile, _localEpisode, false, false);

            result.Should().Be(expected);

            Mocker.GetMock<IDiskTransferService>()
                  .Verify(transfer => transfer.TransferFile(_source, expected, TransferMode.Move, false), Times.Once());

            Mocker.GetMock<IDiskProvider>()
                  .Verify(disk => disk.CreateFolder(It.Is<string>(path => path.PathEquals(Path.Combine(_reviewRoot, "Show Name")))), Times.Once());

            Mocker.GetMock<IDiskProvider>()
                  .Verify(disk => disk.CreateFolder(It.Is<string>(path => path.PathEquals(Path.Combine(_reviewRoot, "Show Name", "Season 01")))), Times.Once());

            Mocker.GetMock<IDiskProvider>()
                  .Verify(disk => disk.CreateFolder(It.Is<string>(path => _seriesPath.IsParentPath(path) || path.PathEquals(_seriesPath))), Times.Never());

            Mocker.GetMock<IRootFolderService>()
                  .Verify(service => service.GetBestRootFolderPath(It.IsAny<string>()), Times.Never());

            Mocker.GetMock<IEventAggregator>()
                  .Verify(aggregator => aggregator.PublishEvent(It.IsAny<EpisodeFolderCreatedEvent>()), Times.Never());
        }

        [Test]
        public void should_omit_the_season_folder_when_the_series_does_not_use_one()
        {
            var libraryPath = Path.Combine(_seriesPath, "Show Name - S01E01.mkv");
            var expected = Path.Combine(_reviewRoot, "Show Name", "Show Name - S01E01.mkv");
            GivenLibraryPath(libraryPath);

            Subject.StageEpisodeFile(_episodeFile, _localEpisode, false, false).Should().Be(expected);

            Mocker.GetMock<IDiskProvider>()
                  .Verify(disk => disk.CreateFolder(It.Is<string>(path => path.PathEquals(Path.Combine(_reviewRoot, "Show Name")))), Times.Once());

            Mocker.GetMock<IDiskProvider>()
                  .Verify(disk => disk.CreateFolder(It.Is<string>(path => path.PathEquals(Path.Combine(_reviewRoot, "Show Name", "Season 01")))), Times.Never());
        }

        private void GivenLibraryPath(string libraryPath)
        {
            Mocker.GetMock<IBuildFileNames>()
                  .Setup(builder => builder.BuildFilePath(It.IsAny<List<Episode>>(), _series, _episodeFile, ".mkv", null, _localEpisode.CustomFormats))
                  .Returns(libraryPath);
        }
    }
}
