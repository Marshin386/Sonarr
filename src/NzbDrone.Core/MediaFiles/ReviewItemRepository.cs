using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.MediaFiles
{
    public interface IReviewItemRepository : IBasicRepository<ReviewItem>
    {
        List<ReviewItem> GetBySeries(int seriesId);
        HashSet<int> GetEpisodeIds();
    }

    public class ReviewItemRepository : BasicRepository<ReviewItem>, IReviewItemRepository
    {
        public ReviewItemRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public List<ReviewItem> GetBySeries(int seriesId)
        {
            return Query(item => item.SeriesId == seriesId).ToList();
        }

        public HashSet<int> GetEpisodeIds()
        {
            return All()
                .Where(item => item.EpisodeIds != null)
                .SelectMany(item => item.EpisodeIds)
                .ToHashSet();
        }
    }
}
