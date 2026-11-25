using System.IO;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles
{
    [TestFixture]
    public class ReviewPathMatcherFixture
    {
        private string _seriesPath;
        private string _reviewRoot;

        [SetUp]
        public void Setup()
        {
            _seriesPath = @"C:\tv\Show Name".AsOsAgnostic();
            _reviewRoot = @"C:\review\tv".AsOsAgnostic();
        }

        [Test]
        public void should_match_the_same_extension()
        {
            var library = Path.Combine(_seriesPath, "Season 01", "Show Name - S01E01.mkv").AsOsAgnostic();
            var review = Path.Combine(_reviewRoot, "Show Name", "Season 01", "Show Name - S01E01.mkv").AsOsAgnostic();

            ReviewPathMatcher.IsAdoptionMatch(_seriesPath, library, review, _reviewRoot).Should().BeTrue();
        }

        [Test]
        public void should_match_when_the_extension_changed()
        {
            var library = Path.Combine(_seriesPath, "Season 01", "Show Name - S01E01.mp4").AsOsAgnostic();
            var review = Path.Combine(_reviewRoot, "Show Name", "Season 01", "Show Name - S01E01.mkv").AsOsAgnostic();

            ReviewPathMatcher.IsAdoptionMatch(_seriesPath, library, review, _reviewRoot).Should().BeTrue();
        }

        [Test]
        public void should_not_match_a_different_name()
        {
            var library = Path.Combine(_seriesPath, "Season 01", "Show Name - S01E02.mp4").AsOsAgnostic();
            var review = Path.Combine(_reviewRoot, "Show Name", "Season 01", "Show Name - S01E01.mkv").AsOsAgnostic();

            ReviewPathMatcher.IsAdoptionMatch(_seriesPath, library, review, _reviewRoot).Should().BeFalse();
        }

        [Test]
        public void should_match_a_multi_episode_file_by_stem()
        {
            var library = Path.Combine(_seriesPath, "Season 01", "Show Name - S01E01-E02.mp4").AsOsAgnostic();
            var review = Path.Combine(_reviewRoot, "Show Name", "Season 01", "Show Name - S01E01-E02.mkv").AsOsAgnostic();

            ReviewPathMatcher.IsAdoptionMatch(_seriesPath, library, review, _reviewRoot).Should().BeTrue();
        }
    }
}
