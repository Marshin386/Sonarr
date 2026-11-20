using System;
using System.Collections.Generic;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.MediaFiles
{
    public class ReviewItem : ModelBase
    {
        public int SeriesId { get; set; }
        public List<int> EpisodeIds { get; set; }
        public string ReviewPath { get; set; }
        public QualityModel Quality { get; set; }
        public List<Language> Languages { get; set; }
        public string ReleaseGroup { get; set; }
        public ReleaseType ReleaseType { get; set; }
        public IndexerFlags IndexerFlags { get; set; }
        public string SceneName { get; set; }
        public string DownloadId { get; set; }
        public DateTime Added { get; set; }
    }
}
