using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Datastore.Events;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Tv;
using NzbDrone.SignalR;
using Sonarr.Http;
using Sonarr.Http.REST;
using Sonarr.Http.REST.Attributes;

namespace Sonarr.Api.V3.Review
{
    public class ReviewEpisodeResource
    {
        public int Id { get; set; }
        public int SeasonNumber { get; set; }
        public int EpisodeNumber { get; set; }
        public string Title { get; set; }
        public bool HasFile { get; set; }
    }

    public class ReviewItemResource : RestResource
    {
        public override string ResourceName => "review";

        public int SeriesId { get; set; }
        public string SeriesTitle { get; set; }
        public List<int> EpisodeIds { get; set; }
        public string ReviewPath { get; set; }
        public List<ReviewEpisodeResource> Episodes { get; set; }
    }

    [V3ApiController]
    public class ReviewController : RestControllerWithSignalR<ReviewItemResource, ReviewItem>
    {
        private readonly IReviewItemService _reviewItemService;
        private readonly ISeriesService _seriesService;
        private readonly IEpisodeService _episodeService;

        public ReviewController(IBroadcastSignalRMessage signalRBroadcaster,
                                IReviewItemService reviewItemService,
                                ISeriesService seriesService,
                                IEpisodeService episodeService)
            : base(signalRBroadcaster)
        {
            _reviewItemService = reviewItemService;
            _seriesService = seriesService;
            _episodeService = episodeService;
        }

        protected override ReviewItemResource GetResourceById(int id)
        {
            return Map(new List<ReviewItem> { _reviewItemService.Get(id) }).Single();
        }

        [HttpGet]
        [Produces("application/json")]
        public List<ReviewItemResource> GetReviewItems()
        {
            return Map(_reviewItemService.All());
        }

        [RestDeleteById]
        public void DeleteReviewItem(int id)
        {
            _reviewItemService.Delete(id);
            BroadcastResourceChange(ModelAction.Deleted, new ReviewItemResource { Id = id });
        }

        private List<ReviewItemResource> Map(List<ReviewItem> items)
        {
            var episodeIds = items.Where(item => item.EpisodeIds != null).SelectMany(item => item.EpisodeIds).Distinct().ToList();
            var episodes = _episodeService.GetEpisodes(episodeIds).ToDictionary(episode => episode.Id);
            var series = _seriesService.GetSeries(items.Select(item => item.SeriesId).Distinct()).ToDictionary(item => item.Id);

            return items.Select(item => new ReviewItemResource
            {
                Id = item.Id,
                SeriesId = item.SeriesId,
                SeriesTitle = series.GetValueOrDefault(item.SeriesId)?.Title,
                EpisodeIds = item.EpisodeIds ?? new List<int>(),
                ReviewPath = item.ReviewPath,
                Episodes = (item.EpisodeIds ?? new List<int>())
                    .Where(episodes.ContainsKey)
                    .Select(id => episodes[id])
                    .Select(episode => new ReviewEpisodeResource
                    {
                        Id = episode.Id,
                        SeasonNumber = episode.SeasonNumber,
                        EpisodeNumber = episode.EpisodeNumber,
                        Title = episode.Title,
                        HasFile = episode.HasFile
                    })
                    .ToList()
            }).ToList();
        }
    }
}
