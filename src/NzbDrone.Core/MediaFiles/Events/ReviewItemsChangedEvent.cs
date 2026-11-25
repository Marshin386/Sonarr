using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.MediaFiles.Events
{
    public class ReviewItemsChangedEvent : IEvent
    {
        public List<int> EpisodeIds { get; private set; }

        public ReviewItemsChangedEvent(IEnumerable<int> episodeIds)
        {
            EpisodeIds = episodeIds?.Distinct().ToList() ?? new List<int>();
        }
    }
}
