import { useMemo } from 'react';
import useApiQuery from 'Helpers/Hooks/useApiQuery';

export interface ReviewEpisode {
  id: number;
  seasonNumber: number;
  episodeNumber: number;
  title?: string;
  hasFile: boolean;
}

export interface ReviewItem {
  id: number;
  seriesId: number;
  seriesTitle?: string;
  episodeIds: number[];
  reviewPath?: string;
  episodes: ReviewEpisode[];
}

export function useReviewItems() {
  return useApiQuery<ReviewItem[]>({
    path: '/review',
  });
}

export function useInReviewCounts(seriesId: number, seasonNumber?: number) {
  const { data } = useReviewItems();

  return useMemo(() => {
    const pendingIds = new Set<number>();
    const episodeIds = new Set<number>();

    (data ?? [])
      .filter((item) => item.seriesId === seriesId)
      .forEach((item) => {
        (item.episodes ?? [])
          .filter(
            (episode) =>
              seasonNumber == null || episode.seasonNumber === seasonNumber
          )
          .forEach((episode) => {
            episodeIds.add(episode.id);

            if (!episode.hasFile) {
              pendingIds.add(episode.id);
            }
          });
      });

    return {
      any: episodeIds.size > 0,
      count: episodeIds.size,
      pendingCount: pendingIds.size,
    };
  }, [data, seriesId, seasonNumber]);
}
