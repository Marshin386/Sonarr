using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.MediaFiles
{
    public interface IKeptEpisodeFileRepository : IBasicRepository<KeptEpisodeFile>
    {
        List<KeptEpisodeFile> GetBySeries(int seriesId);
        KeptEpisodeFile FindByRelativePath(int seriesId, string relativePath);
    }

    public class KeptEpisodeFileRepository : BasicRepository<KeptEpisodeFile>, IKeptEpisodeFileRepository
    {
        public KeptEpisodeFileRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public List<KeptEpisodeFile> GetBySeries(int seriesId)
        {
            return Query(file => file.SeriesId == seriesId).ToList();
        }

        public KeptEpisodeFile FindByRelativePath(int seriesId, string relativePath)
        {
            return Query(file => file.SeriesId == seriesId && file.RelativePath == relativePath).SingleOrDefault();
        }
    }
}
