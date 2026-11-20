using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.EnsureThat;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.MediaFiles.EpisodeImport
{
    public class ImportResult
    {
        public ImportDecision ImportDecision { get; private set; }
        public EpisodeFile EpisodeFile { get; private set; }
        public string ReviewPath { get; private set; }
        public bool StagedForReview { get; private set; }
        public List<string> Errors { get; private set; }

        public ImportResultType Result
        {
            get
            {
                if (Errors.Any())
                {
                    if (ImportDecision.Approved)
                    {
                        return ImportResultType.Skipped;
                    }

                    return ImportResultType.Rejected;
                }

                return ImportResultType.Imported;
            }
        }

        public ImportResult(ImportDecision importDecision, params string[] errors)
        {
            Ensure.That(importDecision, () => importDecision).IsNotNull();

            ImportDecision = importDecision;
            Errors = errors.ToList();
        }

        public ImportResult(ImportDecision importDecision, EpisodeFile episodeFile)
        {
            Ensure.That(importDecision, () => importDecision).IsNotNull();

            ImportDecision = importDecision;
            EpisodeFile = episodeFile;
            Errors = new List<string>();
        }

        public static ImportResult Staged(ImportDecision importDecision, string reviewPath)
        {
            Ensure.That(importDecision, () => importDecision).IsNotNull();
            Ensure.That(reviewPath, () => reviewPath).IsNotNullOrWhiteSpace();

            return new ImportResult(importDecision, reviewPath, stagedForReview: true);
        }

        private ImportResult(ImportDecision importDecision, string reviewPath, bool stagedForReview)
        {
            ImportDecision = importDecision;
            ReviewPath = reviewPath;
            StagedForReview = stagedForReview;
            Errors = new List<string>();
        }
    }
}
