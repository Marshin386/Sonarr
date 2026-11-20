using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.DecisionEngineTests
{
    [TestFixture]
    public class ReviewSpecificationFixture : CoreTest<ReviewSpecification>
    {
        private RemoteEpisode _remoteEpisode;

        [SetUp]
        public void Setup()
        {
            _remoteEpisode = new RemoteEpisode
            {
                Episodes = new List<Episode> { new Episode { Id = 11 }, new Episode { Id = 12 } }
            };

            Mocker.GetMock<IReviewItemService>()
                  .Setup(service => service.GetEpisodeIds())
                  .Returns(new HashSet<int> { 12 });
        }

        [Test]
        public void should_reject_rss_when_an_episode_is_in_review()
        {
            Subject.IsSatisfiedBy(_remoteEpisode, new ReleaseDecisionInformation()).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_reject_automatic_search_when_an_episode_is_in_review()
        {
            var information = new ReleaseDecisionInformation(false, new SingleEpisodeSearchCriteria());

            var decision = Subject.IsSatisfiedBy(_remoteEpisode, information);

            decision.Accepted.Should().BeFalse();
            decision.Reason.Should().Be(DownloadRejectionReason.EpisodeInReview);
            decision.Message.Should().Be("Episode is in review");
        }

        [Test]
        public void should_allow_interactive_search_when_an_episode_is_in_review()
        {
            var information = new ReleaseDecisionInformation(false, new SingleEpisodeSearchCriteria { InteractiveSearch = true });

            Subject.IsSatisfiedBy(_remoteEpisode, information).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_accept_when_no_episode_is_in_review()
        {
            Mocker.GetMock<IReviewItemService>()
                  .Setup(service => service.GetEpisodeIds())
                  .Returns(new HashSet<int>());

            Subject.IsSatisfiedBy(_remoteEpisode, new ReleaseDecisionInformation()).Accepted.Should().BeTrue();
        }
    }
}
