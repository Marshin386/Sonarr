using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.MediaFiles
{
    public interface IReviewItemRepository : IBasicRepository<ReviewItem>
    {
        List<ReviewItem> GetBySeries(int seriesId);
        List<ReviewItem> FindByDownloadId(string downloadId);
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

        public List<ReviewItem> FindByDownloadId(string downloadId)
        {
            return Query(item => item.DownloadId == downloadId).ToList();
        }
    }
}
