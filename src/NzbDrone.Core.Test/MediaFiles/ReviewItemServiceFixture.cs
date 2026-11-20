using System.Collections.Generic;
using System.IO;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles
{
    [TestFixture]
    public class ReviewItemServiceFixture : CoreTest<ReviewItemService>
    {
        [Test]
        public void should_delete_review_files_with_the_same_name_and_any_extension()
        {
            var reviewPath = @"C:\review\tv\Show Name\Season 01\Show.mkv".AsOsAgnostic();
            var folder = Path.GetDirectoryName(reviewPath);
            var mp4 = Path.ChangeExtension(reviewPath, ".mp4");
            var other = Path.Combine(folder, "Other.mkv");
            var item = new ReviewItem
            {
                Id = 5,
                EpisodeIds = new List<int> { 11 },
                ReviewPath = reviewPath
            };

            Mocker.GetMock<IReviewItemRepository>()
                  .Setup(repository => repository.All())
                  .Returns(new List<ReviewItem> { item });

            Mocker.GetMock<IDiskProvider>()
                  .Setup(disk => disk.FolderExists(folder))
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(disk => disk.GetFiles(folder, false))
                  .Returns(new List<string> { reviewPath, mp4, other });

            Subject.RemoveForEpisodes(new[] { 11 });

            Mocker.GetMock<IDiskProvider>().Verify(disk => disk.DeleteFile(reviewPath), Times.Once());
            Mocker.GetMock<IDiskProvider>().Verify(disk => disk.DeleteFile(mp4), Times.Once());
            Mocker.GetMock<IDiskProvider>().Verify(disk => disk.DeleteFile(other), Times.Never());
            Mocker.GetMock<IReviewItemRepository>().Verify(repository => repository.Delete(5), Times.Once());
        }

        [Test]
        public void delete_removes_the_tracking_row_and_leaves_the_file()
        {
            var item = new ReviewItem
            {
                Id = 5,
                EpisodeIds = new List<int> { 11 },
                ReviewPath = @"C:\review\tv\Show Name\Season 01\Show.mkv".AsOsAgnostic()
            };

            Mocker.GetMock<IReviewItemRepository>()
                  .Setup(repository => repository.Get(5))
                  .Returns(item);

            Subject.Delete(5);

            Mocker.GetMock<IDiskProvider>().Verify(disk => disk.DeleteFile(It.IsAny<string>()), Times.Never());
            Mocker.GetMock<IReviewItemRepository>().Verify(repository => repository.Delete(5), Times.Once());
            Mocker.GetMock<IEventAggregator>()
                  .Verify(events => events.PublishEvent(It.Is<ReviewItemsChangedEvent>(message => message.EpisodeIds.Contains(11))), Times.Once());
        }
    }
}
