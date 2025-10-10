using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace MixItUp.Distribution.Core
{
    public static class SafeZipExtractor
    {
        public sealed class ExtractionResult
        {
            public ExtractionResult(IReadOnlyList<string> extractedEntries)
            {
                ExtractedEntries = extractedEntries ?? Array.Empty<string>();
            }

            public IReadOnlyList<string> ExtractedEntries { get; }
        }

        public static ExtractionResult Extract(
            byte[] zipBytes,
            string destinationRoot,
            bool overwriteExisting = true,
            IProgress<int> progress = null,
            Func<ZipArchiveEntry, string> entryPathSelector = null
        )
        {
            if (zipBytes == null || zipBytes.Length == 0)
            {
                throw new DistributionException("Zip archive payload was empty.");
            }

            if (string.IsNullOrWhiteSpace(destinationRoot))
            {
                throw new DistributionException("Destination root path was not provided.");
            }

            string normalizedRoot = Path.GetFullPath(destinationRoot);
            Directory.CreateDirectory(normalizedRoot);

            try
            {
                using (MemoryStream stream = new MemoryStream(zipBytes))
                using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false))
                {
                    if (archive.Entries.Count == 0)
                    {
                        throw new DistributionException("Zip archive contained no entries.");
                    }

                    List<string> extracted = new List<string>(archive.Entries.Count);
                    double processed = 0;
                    double total = archive.Entries.Count;

                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        processed++;
                        string entryPath = entryPathSelector != null
                            ? entryPathSelector(entry)
                            : entry.FullName;

                        if (string.IsNullOrWhiteSpace(entryPath))
                        {
                            ReportProgress(progress, processed, total);
                            continue;
                        }

                        entryPath = entryPath.Replace('/', Path.DirectorySeparatorChar);

                        string destinationPath = Path.GetFullPath(Path.Combine(normalizedRoot, entryPath));
                        if (
                            !destinationPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
                        )
                        {
                            throw new DistributionException(
                                $"Zip archive entry '{entry.FullName}' attempted to write outside of the destination."
                            );
                        }

                        bool isDirectoryEntry = entry.FullName.EndsWith("/", StringComparison.Ordinal)
                            || destinationPath.EndsWith(
                                Path.DirectorySeparatorChar.ToString(),
                                StringComparison.Ordinal
                            );

                        if (isDirectoryEntry)
                        {
                            Directory.CreateDirectory(destinationPath);
                            extracted.Add(destinationPath);
                            ReportProgress(progress, processed, total);
                            continue;
                        }

                        string parent = Path.GetDirectoryName(destinationPath);
                        if (!string.IsNullOrEmpty(parent))
                        {
                            Directory.CreateDirectory(parent);
                        }

                        entry.ExtractToFile(destinationPath, overwriteExisting);
                        extracted.Add(destinationPath);

                        ReportProgress(progress, processed, total);
                    }

                    progress?.Report(100);
                    return new ExtractionResult(extracted);
                }
            }
            catch (InvalidDataException ex)
            {
                throw new DistributionException("Zip archive was invalid or corrupt.", ex);
            }
        }

        private static void ReportProgress(IProgress<int> progress, double processed, double total)
        {
            if (progress == null)
            {
                return;
            }

            if (total <= 0)
            {
                progress.Report(0);
                return;
            }

            int percent = (int)Math.Min(100, Math.Round((processed / total) * 100));
            progress.Report(percent);
        }
    }
}
