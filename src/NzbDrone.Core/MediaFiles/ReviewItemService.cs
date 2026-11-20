using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.MediaFiles
{
    public class ReviewEpisodeState
    {
        public int ReviewItemId { get; set; }
        public string ReviewPath { get; set; }
    }

    public interface IReviewItemService
    {
        ReviewItem Add(ReviewItem item);
        ReviewItem Get(int id);
        List<ReviewItem> GetBySeries(int seriesId);
        List<ReviewItem> All();
        HashSet<int> GetEpisodeIds();
        Dictionary<int, ReviewEpisodeState> GetStatesByEpisodeId();
        void Delete(int id);
        void RemoveForEpisodes(IEnumerable<int> episodeIds);
    }

    public class ReviewItemService : IReviewItemService
    {
        private readonly IReviewItemRepository _repository;
        private readonly IDiskProvider _diskProvider;
        private readonly IEventAggregator _eventAggregator;

        public ReviewItemService(IReviewItemRepository repository, IDiskProvider diskProvider, IEventAggregator eventAggregator)
        {
            _repository = repository;
            _diskProvider = diskProvider;
            _eventAggregator = eventAggregator;
        }

        public ReviewItem Add(ReviewItem item)
        {
            var saved = _repository.Insert(item);
            Publish(item.EpisodeIds);

            return saved;
        }

        public ReviewItem Get(int id)
        {
            return _repository.Get(id);
        }

        public List<ReviewItem> GetBySeries(int seriesId)
        {
            return _repository.GetBySeries(seriesId);
        }

        public List<ReviewItem> All()
        {
            return _repository.All().ToList();
        }

        public HashSet<int> GetEpisodeIds()
        {
            return _repository.GetEpisodeIds();
        }

        public Dictionary<int, ReviewEpisodeState> GetStatesByEpisodeId()
        {
            var states = new Dictionary<int, ReviewEpisodeState>();

            foreach (var item in All().OrderBy(item => item.Added))
            {
                if (item.EpisodeIds == null)
                {
                    continue;
                }

                foreach (var episodeId in item.EpisodeIds)
                {
                    states[episodeId] = new ReviewEpisodeState
                    {
                        ReviewItemId = item.Id,
                        ReviewPath = item.ReviewPath
                    };
                }
            }

            return states;
        }

        public void Delete(int id)
        {
            var item = _repository.Get(id);
            _repository.Delete(id);
            Publish(item.EpisodeIds);
        }

        public void RemoveForEpisodes(IEnumerable<int> episodeIds)
        {
            var ids = episodeIds?.ToHashSet() ?? new HashSet<int>();

            if (ids.Count == 0)
            {
                return;
            }

            var matches = All()
                .Where(item => item.EpisodeIds != null && item.EpisodeIds.Any(ids.Contains))
                .ToList();

            if (matches.Count == 0)
            {
                return;
            }

            var changedEpisodeIds = new List<int>();

            foreach (var item in matches)
            {
                var recordedIds = item.EpisodeIds ?? new List<int>();
                changedEpisodeIds.AddRange(recordedIds);

                var remaining = recordedIds.Where(id => !ids.Contains(id)).Distinct().ToList();

                if (remaining.Count == 0)
                {
                    // The whole file is being replaced. A multi-episode file stays until every episode is released.
                    DeleteFilesMatchingName(item.ReviewPath);
                    _repository.Delete(item.Id);
                }
                else
                {
                    item.EpisodeIds = remaining;
                    _repository.Update(item);
                }
            }

            Publish(changedEpisodeIds);
        }

        private void DeleteFilesMatchingName(string reviewPath)
        {
            if (reviewPath.IsNullOrWhiteSpace())
            {
                return;
            }

            var directory = Path.GetDirectoryName(reviewPath);
            var stem = Path.GetFileNameWithoutExtension(reviewPath);

            if (directory.IsNullOrWhiteSpace() || stem.IsNullOrWhiteSpace() || !_diskProvider.FolderExists(directory))
            {
                return;
            }

            foreach (var file in _diskProvider.GetFiles(directory, false))
            {
                if (stem.Equals(Path.GetFileNameWithoutExtension(file), StringComparison.OrdinalIgnoreCase))
                {
                    _diskProvider.DeleteFile(file);
                }
            }
        }

        private void Publish(IEnumerable<int> episodeIds)
        {
            _eventAggregator.PublishEvent(new ReviewItemsChangedEvent(episodeIds));
        }
    }
}
