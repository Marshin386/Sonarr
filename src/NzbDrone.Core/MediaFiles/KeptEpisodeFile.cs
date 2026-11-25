using System;
using System.Collections.Generic;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.MediaFiles
{
    public class KeptEpisodeFile : ModelBase
    {
        public int SeriesId { get; set; }
        public string RelativePath { get; set; }
        public long Size { get; set; }
        public QualityModel Quality { get; set; }
        public List<Language> Languages { get; set; }
        public string SceneName { get; set; }
        public string ReleaseGroup { get; set; }
        public DateTime DateAdded { get; set; }
    }
}
