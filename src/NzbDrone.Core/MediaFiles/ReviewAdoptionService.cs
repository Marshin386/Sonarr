using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Extras;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MediaFiles.EpisodeImport;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.MediaFiles.MediaInfo;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MediaFiles
{
    public interface IReviewAdoptionService
    {
        List<string> Adopt(Series series, List<string> unmappedFiles);
    }

    public class ReviewAdoptionService : IReviewAdoptionService
    {
        private static readonly HashSet<ImportRejectionReason> UpgradeRejections = new HashSet<ImportRejectionReason>
        {
            ImportRejectionReason.NotQualityUpgrade,
            ImportRejectionReason.NotRevisionUpgrade,
            ImportRejectionReason.NotCustomFormatUpgrade,
            ImportRejectionReason.NotCustomFormatUpgradeAfterRename
        };

        private readonly IReviewItemService _reviewItemService;
        private readonly IKeptEpisodeFileRepository _keptEpisodeFileRepository;
        private readonly IMakeImportDecision _importDecisionMaker;
        private readonly IMediaFileService _mediaFileService;
        private readonly IExtraService _extraService;
        private readonly IDiskProvider _diskProvider;
        private readonly IRecycleBinProvider _recycleBinProvider;
        private readonly IVideoFileInfoReader _videoFileInfoReader;
        private readonly IConfigService _configService;
        private readonly IEventAggregator _eventAggregator;
        private readonly Logger _logger;

        public ReviewAdoptionService(IReviewItemService reviewItemService,
                                     IKeptEpisodeFileRepository keptEpisodeFileRepository,
                                     IMakeImportDecision importDecisionMaker,
                                     IMediaFileService mediaFileService,
                                     IExtraService extraService,
                                     IDiskProvider diskProvider,
                                     IRecycleBinProvider recycleBinProvider,
                                     IVideoFileInfoReader videoFileInfoReader,
                                     IConfigService configService,
                                     IEventAggregator eventAggregator,
                                     Logger logger)
        {
            _reviewItemService = reviewItemService;
            _keptEpisodeFileRepository = keptEpisodeFileRepository;
            _importDecisionMaker = importDecisionMaker;
            _mediaFileService = mediaFileService;
            _extraService = extraService;
            _diskProvider = diskProvider;
            _recycleBinProvider = recycleBinProvider;
            _videoFileInfoReader = videoFileInfoReader;
            _configService = configService;
            _eventAggregator = eventAggregator;
            _logger = logger;
        }

        public List<string> Adopt(Series series, List<string> unmappedFiles)
        {
            if (series == null || unmappedFiles == null || unmappedFiles.Count == 0)
            {
                return unmappedFiles ?? new List<string>();
            }

            var remaining = ExcludeKeptFiles(series, unmappedFiles);
            var reviewItems = (_reviewItemService.GetBySeries(series.Id) ?? new List<ReviewItem>())
                .Where(item => item.EpisodeIds != null && item.EpisodeIds.Count > 0 && item.ReviewPath.IsNotNullOrWhiteSpace())
                .OrderByDescending(item => item.Added)
                .ToList();

            if (reviewItems.Count == 0)
            {
                return remaining;
            }

            var stillUnmapped = new List<string>();

            foreach (var path in remaining)
            {
                var candidates = reviewItems
                    .Where(item => ReviewPathMatcher.IsAdoptionMatch(series.Path, path, item.ReviewPath, _configService.ReviewLocation))
                    .ToList();

                if (candidates.Count == 0)
                {
                    stillUnmapped.Add(path);
                    continue;
                }

                var adopted = false;

                foreach (var candidate in candidates)
                {
                    if (!TryAdopt(series, path, candidate))
                    {
                        continue;
                    }

                    reviewItems.Remove(candidate);
                    adopted = true;
                    break;
                }

                if (!adopted)
                {
                    stillUnmapped.Add(path);
                }
            }

            return stillUnmapped;
        }

        private bool TryAdopt(Series series, string path, ReviewItem reviewItem)
        {
            try
            {
                var decisions = _importDecisionMaker.GetImportDecisions(new List<string> { path }, series, false);
                var decision = decisions?.FirstOrDefault();
                var localEpisode = decision?.LocalEpisode;

                if (localEpisode?.Episodes == null || localEpisode.Episodes.Count == 0)
                {
                    _logger.Debug("Not adopting {0}; the file could not be parsed to episodes", path);
                    return false;
                }

                var parsedIds = localEpisode.Episodes.Select(episode => episode.Id).ToHashSet();
                var recordedIds = reviewItem.EpisodeIds.ToHashSet();

                if (!parsedIds.SetEquals(recordedIds))
                {
                    _logger.Debug("Not adopting {0}; parsed episodes {1} do not match review item {2}", path, parsedIds.Count, reviewItem.Id);
                    return false;
                }

                if (decision.Rejections.Any(rejection => !UpgradeRejections.Contains(rejection.Reason)))
                {
                    _logger.Debug("Not adopting {0}; {1}", path, string.Join(", ", decision.Rejections.Select(rejection => rejection.Reason)));
                    return false;
                }

                localEpisode.Series = series;
                localEpisode.Path = path;
                localEpisode.Quality = reviewItem.Quality;
                localEpisode.Languages = reviewItem.Languages ?? new List<Language>();
                localEpisode.ReleaseGroup = reviewItem.ReleaseGroup;
                localEpisode.SceneName = reviewItem.SceneName;
                localEpisode.IndexerFlags = reviewItem.IndexerFlags;
                localEpisode.ReleaseType = reviewItem.ReleaseType;

                var mediaInfo = _videoFileInfoReader.GetMediaInfo(path);

                if (mediaInfo != null)
                {
                    localEpisode.MediaInfo = mediaInfo;
                }

                var episodeFile = localEpisode.ToEpisodeFile();
                episodeFile.Size = _diskProvider.GetFileSize(path);
                var oldFiles = RetireExistingFiles(series, localEpisode, episodeFile.RelativePath);
                episodeFile = _mediaFileService.Add(episodeFile);

                _extraService.ImportEpisode(localEpisode, episodeFile, true);

                var downloadClientItem = reviewItem.DownloadId.IsNullOrWhiteSpace()
                    ? null
                    : new DownloadClientItem { DownloadId = reviewItem.DownloadId };

                _eventAggregator.PublishEvent(new EpisodeImportedEvent(localEpisode, episodeFile, oldFiles, true, downloadClientItem));
                _eventAggregator.PublishEvent(new UntrackedDownloadCompletedEvent(
                    series,
                    localEpisode.Episodes.ToList(),
                    new List<EpisodeFile> { episodeFile },
                    ParsedEpisodeInfoFor(localEpisode, reviewItem),
                    path));

                _reviewItemService.RemoveForEpisodes(reviewItem.EpisodeIds);
                _logger.Info("Adopted {0} from review item {1}", episodeFile.RelativePath, reviewItem.Id);

                return true;
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Couldn't adopt review file {0}", path);
                return false;
            }
        }

        private List<string> ExcludeKeptFiles(Series series, List<string> unmappedFiles)
        {
            var kept = _keptEpisodeFileRepository.GetBySeries(series.Id) ?? new List<KeptEpisodeFile>();

            if (kept.Count == 0 || series.Path.IsNullOrWhiteSpace())
            {
                return unmappedFiles.ToList();
            }

            return unmappedFiles
                .Where(path =>
                {
                    if (series.Path.IsNullOrWhiteSpace() || !series.Path.IsParentPath(path))
                    {
                        return true;
                    }

                    var relative = series.Path.GetRelativePath(path);

                    return kept.All(file => !ReviewPathMatcher.RelativePathsEqual(file.RelativePath, relative));
                })
                .ToList();
        }

        private List<DeletedEpisodeFile> RetireExistingFiles(Series series, LocalEpisode localEpisode, string adoptedRelativePath)
        {
            var oldFiles = new List<DeletedEpisodeFile>();
            var existingFiles = localEpisode.Episodes
                .Where(episode => episode.EpisodeFileId > 0 && episode.EpisodeFile?.Value != null)
                .Select(episode => episode.EpisodeFile.Value)
                .GroupBy(file => file.Id)
                .Select(group => group.First())
                .ToList();

            if (existingFiles.Count == 0)
            {
                return oldFiles;
            }

            var rootFolder = _diskProvider.GetParentFolder(series.Path);
            var mustRecycle = !_configService.KeepOriginalFile &&
                              existingFiles.Any(file => !ReviewPathMatcher.RelativePathsEqual(file.RelativePath, adoptedRelativePath));

            if (mustRecycle && !_diskProvider.FolderExists(rootFolder))
            {
                throw new RootFolderNotFoundException($"Root folder '{rootFolder}' was not found.");
            }

            foreach (var file in existingFiles)
            {
                var episodeFilePath = Path.Combine(series.Path, file.RelativePath);
                string recycleBinPath = null;

                if (ReviewPathMatcher.RelativePathsEqual(file.RelativePath, adoptedRelativePath))
                {
                    _logger.Debug("Existing episode file is the file being adopted, leaving it on disk: {0}", episodeFilePath);
                }
                else if (_configService.KeepOriginalFile)
                {
                    RememberKeptFile(series, file);
                }
                else if (_diskProvider.FileExists(episodeFilePath))
                {
                    var subfolder = rootFolder.GetRelativePath(_diskProvider.GetParentFolder(episodeFilePath));
                    _logger.Debug("Recycling existing episode file: {0}", file);
                    recycleBinPath = _recycleBinProvider.DeleteFile(episodeFilePath, subfolder);
                }

                oldFiles.Add(new DeletedEpisodeFile(file, recycleBinPath));
                _mediaFileService.Delete(file, DeleteMediaFileReason.Upgrade);
            }

            return oldFiles;
        }

        private void RememberKeptFile(Series series, EpisodeFile file)
        {
            if (_keptEpisodeFileRepository.FindByRelativePath(series.Id, file.RelativePath) != null)
            {
                return;
            }

            _keptEpisodeFileRepository.Insert(new KeptEpisodeFile
            {
                SeriesId = series.Id,
                RelativePath = file.RelativePath,
                Size = file.Size,
                Quality = file.Quality,
                Languages = file.Languages,
                SceneName = file.SceneName,
                ReleaseGroup = file.ReleaseGroup,
                DateAdded = DateTime.UtcNow
            });
        }

        private static ParsedEpisodeInfo ParsedEpisodeInfoFor(LocalEpisode localEpisode, ReviewItem reviewItem)
        {
            var parsed = localEpisode.FileEpisodeInfo ?? new ParsedEpisodeInfo();
            parsed.Quality = reviewItem.Quality;
            parsed.ReleaseGroup = reviewItem.ReleaseGroup;

            return parsed;
        }
    }
}
