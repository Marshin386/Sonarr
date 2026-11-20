using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Extras;
using NzbDrone.Core.History;
using NzbDrone.Core.MediaFiles.Commands;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.MediaFiles.EpisodeImport
{
    public interface IImportApprovedEpisodes
    {
        List<ImportResult> Import(List<ImportDecision> decisions, bool newDownload, DownloadClientItem downloadClientItem = null, ImportMode importMode = ImportMode.Auto);
    }

    public class ImportApprovedEpisodes : IImportApprovedEpisodes
    {
        private readonly IUpgradeMediaFiles _episodeFileUpgrader;
        private readonly IMoveEpisodeFiles _episodeFileMover;
        private readonly IMediaFileService _mediaFileService;
        private readonly IReviewItemService _reviewItemService;
        private readonly IExtraService _extraService;
        private readonly IExistingExtraFiles _existingExtraFiles;
        private readonly IDiskProvider _diskProvider;
        private readonly IHistoryService _historyService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public ImportApprovedEpisodes(IUpgradeMediaFiles episodeFileUpgrader,
                                      IMoveEpisodeFiles episodeFileMover,
                                      IMediaFileService mediaFileService,
                                      IReviewItemService reviewItemService,
                                      IExtraService extraService,
                                      IExistingExtraFiles existingExtraFiles,
                                      IDiskProvider diskProvider,
                                      IHistoryService historyService,
                                      IEventAggregator eventAggregator,
                                      IManageCommandQueue commandQueueManager,
                                      IConfigService configService,
                                      Logger logger)
        {
            _episodeFileUpgrader = episodeFileUpgrader;
            _episodeFileMover = episodeFileMover;
            _mediaFileService = mediaFileService;
            _reviewItemService = reviewItemService;
            _extraService = extraService;
            _existingExtraFiles = existingExtraFiles;
            _diskProvider = diskProvider;
            _historyService = historyService;
            _eventAggregator = eventAggregator;
            _commandQueueManager = commandQueueManager;
            _configService = configService;
            _logger = logger;
        }

        public List<ImportResult> Import(List<ImportDecision> decisions, bool newDownload, DownloadClientItem downloadClientItem = null, ImportMode importMode = ImportMode.Auto)
        {
            var qualifiedImports = decisions
                .Where(decision => decision.Approved)
                .GroupBy(decision => decision.LocalEpisode.Series.Id)
                .SelectMany(group => group
                    .OrderByDescending(decision => decision.LocalEpisode.Quality, new QualityModelComparer(group.First().LocalEpisode.Series.QualityProfile))
                    .ThenByDescending(decision => decision.LocalEpisode.Size))
                .ToList();

            var importResults = new List<ImportResult>();

            foreach (var importDecision in qualifiedImports.OrderBy(e => e.LocalEpisode.Episodes.Select(episode => episode.EpisodeNumber).MinOrDefault())
                                                           .ThenByDescending(e => e.LocalEpisode.Size))
            {
                var localEpisode = importDecision.LocalEpisode;
                var oldFiles = new List<DeletedEpisodeFile>();

                try
                {
                    // check if already imported
                    if (importResults.SelectMany(r => r.ImportDecision.LocalEpisode.Episodes)
                                         .Select(e => e.Id)
                                         .Intersect(localEpisode.Episodes.Select(e => e.Id))
                                         .Any())
                    {
                        importResults.Add(new ImportResult(importDecision, "Episode has already been imported"));
                        continue;
                    }

                    var episodeFile = localEpisode.ToEpisodeFile();
                    episodeFile.Size = _diskProvider.GetFileSize(localEpisode.Path);

                    if (downloadClientItem?.DownloadId.IsNotNullOrWhiteSpace() == true)
                    {
                        var grabHistory = _historyService.FindByDownloadId(downloadClientItem.DownloadId)
                            .OrderByDescending(h => h.Date)
                            .FirstOrDefault(h => h.EventType == EpisodeHistoryEventType.Grabbed);

                        if (Enum.TryParse(grabHistory?.Data.GetValueOrDefault("indexerFlags"), true, out IndexerFlags flags))
                        {
                            episodeFile.IndexerFlags = flags;
                        }

                        // Prefer the release type from the grabbed history
                        if (Enum.TryParse(grabHistory?.Data.GetValueOrDefault("releaseType"), true, out ReleaseType releaseType) &&
                            releaseType != ReleaseType.Unknown)
                        {
                            episodeFile.ReleaseType = releaseType;
                        }
                    }

                    bool copyOnly;
                    switch (importMode)
                    {
                        default:
                        case ImportMode.Auto:
                            copyOnly = downloadClientItem is { CanMoveFiles: false };
                            break;
                        case ImportMode.Move:
                            copyOnly = false;
                            break;
                        case ImportMode.Copy:
                            copyOnly = true;
                            break;
                    }

                    if (ShouldStageForReview(localEpisode))
                    {
                        var reviewPath = _episodeFileMover.StageEpisodeFile(episodeFile, localEpisode, copyOnly);

                        _reviewItemService.Add(new ReviewItem
                        {
                            SeriesId = localEpisode.Series.Id,
                            EpisodeIds = localEpisode.Episodes.Select(episode => episode.Id).Distinct().ToList(),
                            ReviewPath = reviewPath,
                            Quality = episodeFile.Quality,
                            Languages = episodeFile.Languages ?? [],
                            ReleaseGroup = episodeFile.ReleaseGroup,
                            ReleaseType = episodeFile.ReleaseType,
                            IndexerFlags = episodeFile.IndexerFlags,
                            SceneName = episodeFile.SceneName,
                            DownloadId = downloadClientItem?.DownloadId,
                            Added = DateTime.UtcNow
                        });

                        // TODO: Round 3 adopts the file from the review folder into the series folder
                        // (the extension may have changed), writes history, extras, and metadata, and
                        // replaces the previous episode file. Until then a staged import has no EpisodeFile.
                        _logger.Info("Staged episode import for review at {0}", reviewPath);
                        importResults.Add(ImportResult.Staged(importDecision, reviewPath));
                        continue;
                    }

                    if (newDownload)
                    {
                        if (downloadClientItem is { OutputPath.IsEmpty: false })
                        {
                            var outputDirectory = downloadClientItem.OutputPath.Directory.ToString();

                            if (outputDirectory.IsParentPath(localEpisode.Path))
                            {
                                episodeFile.OriginalFilePath = outputDirectory.GetRelativePath(localEpisode.Path);
                            }
                        }

                        oldFiles = _episodeFileUpgrader.UpgradeEpisodeFile(episodeFile, localEpisode, copyOnly).OldFiles;
                    }
                    else
                    {
                        // Delete existing files from the DB mapped to this path
                        var previousFiles = _mediaFileService.GetFilesWithRelativePath(localEpisode.Series.Id, episodeFile.RelativePath);

                        foreach (var previousFile in previousFiles)
                        {
                            _mediaFileService.Delete(previousFile, DeleteMediaFileReason.ManualOverride);
                        }
                    }

                    episodeFile = _mediaFileService.Add(episodeFile);
                    importResults.Add(new ImportResult(importDecision, episodeFile));

                    if (newDownload)
                    {
                        if (localEpisode.ScriptImported)
                        {
                            _existingExtraFiles.ImportExtraFiles(localEpisode.Series, localEpisode.PossibleExtraFiles, localEpisode.FileNameBeforeRename);

                            if (localEpisode.FileNameBeforeRename != episodeFile.RelativePath)
                            {
                                _extraService.MoveFilesAfterRename(localEpisode.Series, episodeFile);
                            }
                        }

                        if (!localEpisode.ScriptImported || localEpisode.ShouldImportExtras)
                        {
                            _extraService.ImportEpisode(localEpisode, episodeFile, copyOnly);
                        }
                    }

                    _eventAggregator.PublishEvent(new EpisodeImportedEvent(localEpisode, episodeFile, oldFiles, newDownload, downloadClientItem));
                }
                catch (RootFolderNotFoundException e)
                {
                    _logger.Warn(e, "Couldn't import episode " + localEpisode);
                    _eventAggregator.PublishEvent(new EpisodeImportFailedEvent(e, localEpisode, newDownload, downloadClientItem));

                    importResults.Add(new ImportResult(importDecision, "Failed to import episode, Root folder missing."));
                }
                catch (DestinationAlreadyExistsException e)
                {
                    _logger.Warn(e, "Couldn't import episode " + localEpisode);
                    importResults.Add(new ImportResult(importDecision, "Failed to import episode, Destination already exists."));

                    _commandQueueManager.Push(new RescanSeriesCommand(localEpisode.Series.Id));
                }
                catch (RecycleBinException e)
                {
                    _logger.Warn(e, "Couldn't import episode " + localEpisode);
                    _eventAggregator.PublishEvent(new EpisodeImportFailedEvent(e, localEpisode, newDownload, downloadClientItem));

                    importResults.Add(new ImportResult(importDecision, "Failed to import episode, unable to move existing file to the Recycle Bin."));
                }
                catch (UnauthorizedAccessException e)
                {
                    _logger.Warn(e, "Couldn't import episode " + localEpisode);
                    _eventAggregator.PublishEvent(new EpisodeImportFailedEvent(e, localEpisode, newDownload, downloadClientItem));

                    importResults.Add(new ImportResult(importDecision, "Failed to import episode due to permission issues.", e.Message));
                }
                catch (Exception e)
                {
                    _logger.Warn(e, "Couldn't import episode " + localEpisode);
                    importResults.Add(new ImportResult(importDecision, "Failed to import episode", e.Message));
                }
            }

            // Adding all the rejected decisions
            importResults.AddRange(decisions.Where(c => !c.Approved)
                                            .Select(d => new ImportResult(d, d.Rejections.Select(r => r.Message).ToArray())));

            return importResults;
        }

        private bool ShouldStageForReview(LocalEpisode localEpisode)
        {
            if (!_configService.ReviewLocationEnabled)
            {
                return false;
            }

            // Files already inside the series folder are library scans or in-place imports.
            // Leave those on the normal import path so a later adoption rescan can match them.
            if (localEpisode.Series?.Path.IsNotNullOrWhiteSpace() == true &&
                localEpisode.Series.Path.IsParentPath(localEpisode.Path))
            {
                return false;
            }

            return true;
        }
    }
}
