using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(241)]
    public class add_review_items : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.TableForModel("ReviewItems")
                  .WithColumn("SeriesId").AsInt32().NotNullable().Indexed()
                  .WithColumn("EpisodeIds").AsString().NotNullable()
                  .WithColumn("ReviewPath").AsString().NotNullable().Unique()
                  .WithColumn("Quality").AsString().NotNullable()
                  .WithColumn("Languages").AsString().NotNullable()
                  .WithColumn("ReleaseGroup").AsString().Nullable()
                  .WithColumn("ReleaseType").AsInt32().NotNullable().WithDefaultValue(0)
                  .WithColumn("IndexerFlags").AsInt32().NotNullable().WithDefaultValue(0)
                  .WithColumn("SceneName").AsString().Nullable()
                  .WithColumn("DownloadId").AsString().Nullable()
                  .WithColumn("Added").AsDateTime().NotNullable();

            Create.TableForModel("KeptEpisodeFiles")
                  .WithColumn("SeriesId").AsInt32().NotNullable()
                  .WithColumn("RelativePath").AsString().NotNullable()
                  .WithColumn("Size").AsInt64().NotNullable()
                  .WithColumn("Quality").AsString().NotNullable()
                  .WithColumn("Languages").AsString().NotNullable()
                  .WithColumn("SceneName").AsString().Nullable()
                  .WithColumn("ReleaseGroup").AsString().Nullable()
                  .WithColumn("DateAdded").AsDateTime().NotNullable();

            Create.Index()
                  .OnTable("KeptEpisodeFiles")
                  .OnColumn("SeriesId").Ascending()
                  .OnColumn("RelativePath").Ascending()
                  .WithOptions().Unique();
        }
    }
}
