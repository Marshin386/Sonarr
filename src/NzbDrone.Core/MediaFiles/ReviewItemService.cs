using System.Collections.Generic;
using System.Linq;

namespace NzbDrone.Core.MediaFiles
{
    public interface IReviewItemService
    {
        ReviewItem Add(ReviewItem item);
        List<ReviewItem> GetBySeries(int seriesId);
        List<ReviewItem> All();
        void Delete(int id);
    }

    public class ReviewItemService : IReviewItemService
    {
        private readonly IReviewItemRepository _repository;

        public ReviewItemService(IReviewItemRepository repository)
        {
            _repository = repository;
        }

        public ReviewItem Add(ReviewItem item)
        {
            return _repository.Insert(item);
        }

        public List<ReviewItem> GetBySeries(int seriesId)
        {
            return _repository.GetBySeries(seriesId);
        }

        public List<ReviewItem> All()
        {
            return _repository.All().ToList();
        }

        public void Delete(int id)
        {
            _repository.Delete(id);
        }
    }
}
