using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Test.Common;
using Sonarr.Api.V5.Review;

namespace NzbDrone.Api.Test.v5.Review
{
    [TestFixture]
    public class ReviewControllerFixture : TestBase<ReviewController>
    {
        [Test]
        public void delete_removes_the_tracking_entry_and_does_not_delete_the_file()
        {
            Subject.DeleteReviewItem(5);

            Mocker.GetMock<IReviewItemService>().Verify(service => service.Delete(5), Times.Once());
            Mocker.GetMock<IDiskProvider>().Verify(disk => disk.DeleteFile(It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void list_includes_series_and_episode_info()
        {
            Mocker.GetMock<IReviewItemService>()
                  .Setup(service => service.All())
                  .Returns(new List<ReviewItem>
                  {
                      new ReviewItem
                      {
                          Id = 5,
                          SeriesId = 4,
                          EpisodeIds = new List<int> { 11 },
                          ReviewPath = "/review/tv/Show Name/Season 01/Show.mkv"
                      }
                  });

            Mocker.GetMock<NzbDrone.Core.Tv.IEpisodeService>()
                  .Setup(service => service.GetEpisodes(It.IsAny<IEnumerable<int>>()))
                  .Returns(new List<NzbDrone.Core.Tv.Episode>
                  {
                      new NzbDrone.Core.Tv.Episode { Id = 11, SeasonNumber = 1, EpisodeNumber = 2, Title = "Pilot" }
                  });

            Mocker.GetMock<NzbDrone.Core.Tv.ISeriesService>()
                  .Setup(service => service.GetSeries(It.IsAny<IEnumerable<int>>()))
                  .Returns(new List<NzbDrone.Core.Tv.Series>
                  {
                      new NzbDrone.Core.Tv.Series { Id = 4, Title = "Show Name" }
                  });

            var items = Subject.GetReviewItems().Value;

            items.Should().ContainSingle();
            items[0].SeriesTitle.Should().Be("Show Name");
            items[0].ReviewPath.Should().Be("/review/tv/Show Name/Season 01/Show.mkv");
            items[0].Episodes.Should().ContainSingle().Which.EpisodeNumber.Should().Be(2);
        }
    }
}
