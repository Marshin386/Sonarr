using System.Linq;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    public class ReviewSpecification : IDownloadDecisionEngineSpecification
    {
        private readonly IReviewItemService _reviewItemService;

        public ReviewSpecification(IReviewItemService reviewItemService)
        {
            _reviewItemService = reviewItemService;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public DownloadSpecDecision IsSatisfiedBy(RemoteEpisode subject, ReleaseDecisionInformation information)
        {
            // Interactive Search is the manual override, same as DelaySpecification skipping
            // user-invoked searches. The release stays approved so there is no rejection
            // banner. RSS sync and every automatic search (episode, season, series, missing,
            // cutoff unmet, search on add) still reject.
            if (information?.SearchCriteria is { InteractiveSearch: true })
            {
                return DownloadSpecDecision.Accept();
            }

            if (subject?.Episodes == null || subject.Episodes.Count == 0)
            {
                return DownloadSpecDecision.Accept();
            }

            var inReview = _reviewItemService.GetEpisodeIds();

            if (subject.Episodes.Any(episode => inReview.Contains(episode.Id)))
            {
                return DownloadSpecDecision.Reject(DownloadRejectionReason.EpisodeInReview, "Episode is in review");
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
