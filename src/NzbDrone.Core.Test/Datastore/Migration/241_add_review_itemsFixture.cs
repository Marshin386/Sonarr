using System;
using System.Data;
using Dapper;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration
{
    [TestFixture]
    public class add_review_itemsFixture : MigrationTest<add_review_items>
    {
        [Test]
        public void should_create_review_items_and_kept_episode_files()
        {
            var db = WithDapperMigrationTestDb();

            var added = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

            db.Execute(
                @"INSERT INTO ""ReviewItems""
                    (""SeriesId"", ""EpisodeIds"", ""ReviewPath"", ""Quality"", ""Languages"", ""ReleaseGroup"", ""ReleaseType"", ""IndexerFlags"", ""SceneName"", ""DownloadId"", ""Added"")
                  VALUES
                    (@SeriesId, @EpisodeIds, @ReviewPath, @Quality, @Languages, @ReleaseGroup, @ReleaseType, @IndexerFlags, @SceneName, @DownloadId, @Added)",
                new
                {
                    SeriesId = 4,
                    EpisodeIds = "[11,12]",
                    ReviewPath = "/review/tv/Show Name/Season 01/Show Name - S01E01E02.mkv",
                    Quality = "{\"quality\": 3}",
                    Languages = "[1]",
                    ReleaseGroup = "Group",
                    ReleaseType = 2,
                    IndexerFlags = 1,
                    SceneName = "Show.Name.S01E01E02",
                    DownloadId = "download-1",
                    Added = added
                });

            db.Execute(
                @"INSERT INTO ""KeptEpisodeFiles""
                    (""SeriesId"", ""RelativePath"", ""Size"", ""Quality"", ""Languages"", ""SceneName"", ""ReleaseGroup"", ""DateAdded"")
                  VALUES
                    (@SeriesId, @RelativePath, @Size, @Quality, @Languages, @SceneName, @ReleaseGroup, @DateAdded)",
                new
                {
                    SeriesId = 4,
                    RelativePath = "Season 01/Show Name - S01E01.old.mkv",
                    Size = 1234L,
                    Quality = "{\"quality\": 1}",
                    Languages = "[1]",
                    SceneName = "Show.Name.S01E01.old",
                    ReleaseGroup = "OldGroup",
                    DateAdded = added
                });

            db.QuerySingle<int>(@"SELECT ""SeriesId"" FROM ""ReviewItems""").Should().Be(4);
            db.QuerySingle<string>(@"SELECT ""EpisodeIds"" FROM ""ReviewItems""").Should().Be("[11,12]");
            db.QuerySingle<string>(@"SELECT ""ReviewPath"" FROM ""ReviewItems""")
                .Should().Be("/review/tv/Show Name/Season 01/Show Name - S01E01E02.mkv");
            db.QuerySingle<string>(@"SELECT ""DownloadId"" FROM ""ReviewItems""").Should().Be("download-1");

            var kept = db.QuerySingle<string>(@"SELECT ""RelativePath"" FROM ""KeptEpisodeFiles"" WHERE ""SeriesId"" = 4");
            kept.Should().Be("Season 01/Show Name - S01E01.old.mkv");
        }

        [Test]
        public void should_reject_a_duplicate_kept_episode_path()
        {
            var db = WithDapperMigrationTestDb();
            var added = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

            InsertKept(db, added);
            Action insertAgain = () => InsertKept(db, added);

            insertAgain.Should().Throw<Exception>();
        }

        private static void InsertKept(IDbConnection db, DateTime added)
        {
            db.Execute(
                @"INSERT INTO ""KeptEpisodeFiles""
                    (""SeriesId"", ""RelativePath"", ""Size"", ""Quality"", ""Languages"", ""SceneName"", ""ReleaseGroup"", ""DateAdded"")
                  VALUES
                    (4, 'Season 01/Show Name - S01E01.old.mkv', 10, '{""quality"": 1}', '[1]', 'scene', 'group', @DateAdded)",
                new { DateAdded = added });
        }
    }
}
