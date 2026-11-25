using System.Collections.Generic;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;
using Sonarr.Api.V5.Episodes;

namespace NzbDrone.Api.Test.v5.Episodes
{
    [TestFixture]
    public class EpisodeReviewFlagFixture : TestBase<EpisodeController>
    {
        [Test]
        public void should_set_in_review_from_the_review_item()
        {
            var episode = new Episode
            {
                Id = 11,
                SeriesId = 4,
                Series = new Series { Id = 4 }
            };

            Mocker.GetMock<IEpisodeService>()
                  .Setup(service => service.GetEpisode(11))
                  .Returns(episode);

            Mocker.GetMock<IReviewItemService>()
                  .Setup(service => service.GetStatesByEpisodeId())
                  .Returns(new Dictionary<int, ReviewEpisodeState>
                  {
                      [11] = new ReviewEpisodeState
                      {
                          ReviewItemId = 8,
                          ReviewPath = "/review/tv/Show Name/Season 01/Show.mkv"
                      }
                  });

            var result = Subject.GetResourceByIdWithErrorHandler(11);
            var resource = result.Result.Should().BeOfType<Ok<EpisodeResource>>().Subject.Value;

            resource.InReview.Should().BeTrue();
            resource.ReviewItemId.Should().Be(8);
            resource.ReviewPath.Should().Be("/review/tv/Show Name/Season 01/Show.mkv");
        }
    }
}
