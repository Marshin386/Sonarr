using System.Collections.Generic;
using System.IO;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.MediaFiles
{
    public static class ReviewPathMatcher
    {
        public static bool IsAdoptionMatch(string seriesPath, string libraryFilePath, string reviewPath, string reviewRoot)
        {
            if (seriesPath.IsNullOrWhiteSpace() || libraryFilePath.IsNullOrWhiteSpace() || reviewPath.IsNullOrWhiteSpace())
            {
                return false;
            }

            if (!seriesPath.IsParentPath(libraryFilePath))
            {
                return false;
            }

            var libraryRelative = seriesPath.GetRelativePath(libraryFilePath);
            var reviewRelative = RelativePathUnderSeriesFolder(reviewPath, seriesPath, reviewRoot);

            return RelativeStemsMatch(libraryRelative, reviewRelative);
        }

        public static bool RelativePathsEqual(string left, string right)
        {
            if (left.IsNullOrWhiteSpace() || right.IsNullOrWhiteSpace())
            {
                return false;
            }

            return string.Equals(NormalizeRelative(left), NormalizeRelative(right), DiskProviderBase.PathStringComparison);
        }

        public static bool RelativeStemsMatch(string leftRelative, string rightRelative)
        {
            if (leftRelative.IsNullOrWhiteSpace() || rightRelative.IsNullOrWhiteSpace())
            {
                return false;
            }

            return RelativePathsEqual(Stem(leftRelative), Stem(rightRelative));
        }

        public static string NormalizeRelative(string path)
        {
            return path.Replace('\\', '/').Trim('/');
        }

        public static string Stem(string relativePath)
        {
            var directory = Path.GetDirectoryName(relativePath) ?? string.Empty;
            var fileName = Path.GetFileNameWithoutExtension(relativePath);

            if (directory.IsNullOrWhiteSpace())
            {
                return fileName ?? string.Empty;
            }

            return Path.Combine(directory, fileName ?? string.Empty);
        }

        public static string RelativePathUnderSeriesFolder(string reviewPath, string seriesPath, string reviewRoot)
        {
            var seriesFolderName = new OsPath(seriesPath).Name;

            if (seriesFolderName.IsNullOrWhiteSpace())
            {
                return null;
            }

            if (reviewRoot.IsNotNullOrWhiteSpace())
            {
                var prefix = Path.Combine(reviewRoot, seriesFolderName);

                if (prefix.IsParentPath(reviewPath))
                {
                    return prefix.GetRelativePath(reviewPath);
                }
            }

            var parts = new List<string>();
            var current = new OsPath(reviewPath);
            var foundSeriesFolder = false;

            for (var depth = 0; depth < 64 && current != OsPath.Null && current.FileName.IsNotNullOrWhiteSpace(); depth++)
            {
                if (string.Equals(current.FileName, seriesFolderName, DiskProviderBase.PathStringComparison))
                {
                    foundSeriesFolder = true;
                    break;
                }

                parts.Insert(0, current.FileName);

                var parent = current.Directory;

                if (parent == OsPath.Null || string.Equals(parent.FullPath, current.FullPath, DiskProviderBase.PathStringComparison))
                {
                    break;
                }

                current = parent;
            }

            if (!foundSeriesFolder || parts.Count == 0)
            {
                return null;
            }

            return Path.Combine(parts.ToArray());
        }
    }
}
