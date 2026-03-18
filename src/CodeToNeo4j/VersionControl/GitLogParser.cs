using System.IO.Abstractions;
using CodeToNeo4j.FileSystem;

namespace CodeToNeo4j.VersionControl;

public class GitLogParser(
    IFileService fileService,
    IFileSystem fileSystem) : IGitLogParser
{
    public IEnumerable<CommitMetadata> ParseCommits(string output, string repoRoot)
    {
        var commits = new List<CommitMetadata>();
        var lines = output.Split('\n', StringSplitOptions.TrimEntries);
        CommitMetadata? currentCommit = null;
        var changedFiles = new List<FileStatus>();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith("COMMIT|"))
            {
                if (currentCommit != null)
                {
                    commits.Add(currentCommit with { ChangedFiles = [.. changedFiles] });
                    changedFiles = [];
                }

                var headerParts = line["COMMIT|".Length..].Split("|#|", 5, StringSplitOptions.None);
                if (headerParts.Length >= 5)
                {
                    if (DateTimeOffset.TryParse(headerParts[3], out var date))
                    {
                        currentCommit = new CommitMetadata(headerParts[0], headerParts[1], headerParts[2], date, headerParts[4], []);
                    }
                }
            }
            else
            {
                if (currentCommit != null)
                {
                    var fileParts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
                    if (fileParts.Length >= 2)
                    {
                        var status = fileParts[0];
                        var relPath = fileParts[1];
                        var fullPath = fileService.NormalizePath(fileSystem.Path.Combine(repoRoot, relPath));
                        changedFiles.Add(new FileStatus(fullPath, status.StartsWith('D')));
                    }
                }
            }
        }

        if (currentCommit != null)
        {
            commits.Add(currentCommit with { ChangedFiles = [.. changedFiles] });
        }

        return commits;
    }

    public FileMetadata BuildFileMetadata(
        IList<(string Author, DateTimeOffset Date, string Hash, string? Refs)> history)
    {
        var authorMap = new Dictionary<string, (DateTimeOffset first, DateTimeOffset last, int count)>();
        var created = DateTimeOffset.MaxValue;
        var lastModified = DateTimeOffset.MinValue;
        var hashes = new List<string>();
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var commit in history)
        {
            if (commit.Date < created)
            {
                created = commit.Date;
            }

            if (commit.Date > lastModified)
            {
                lastModified = commit.Date;
            }

            if (authorMap.TryGetValue(commit.Author, out var stats))
            {
                var newFirst = commit.Date < stats.first ? commit.Date : stats.first;
                var newLast = commit.Date > stats.last ? commit.Date : stats.last;
                authorMap[commit.Author] = (newFirst, newLast, stats.count + 1);
            }
            else
            {
                authorMap[commit.Author] = (commit.Date, commit.Date, 1);
            }

            hashes.Add(commit.Hash);

            if (commit.Refs != null)
            {
                var refs = commit.Refs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var r in refs)
                {
                    if (r.StartsWith("tag:"))
                    {
                        tags.Add(r[4..].Trim());
                    }
                }
            }
        }

        var authors = authorMap.Select(m => new AuthorMetadata(m.Key, m.Value.first, m.Value.last, m.Value.count)).ToArray();
        return new FileMetadata(created, lastModified, authors, [.. hashes], [.. tags]);
    }

    public FileMetadata ParseSingleFileLog(string output)
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0)
        {
            return new FileMetadata(DateTimeOffset.MinValue, DateTimeOffset.MinValue, [], [], []);
        }

        var history = new List<(string Author, DateTimeOffset Date, string Hash, string? Refs)>();

        foreach (var line in lines)
        {
            var parts = line.Split('|');
            if (parts.Length < 2)
            {
                continue;
            }

            var authorName = parts[0];
            if (!DateTimeOffset.TryParse(parts[1], out var commitDate))
            {
                continue;
            }

            var hash = parts.Length >= 3 ? parts[2] : "";
            var refs = parts.Length >= 4 && !string.IsNullOrWhiteSpace(parts[3]) ? parts[3] : null;

            history.Add((authorName, commitDate, hash, refs));
        }

        return history.Count == 0
            ? new FileMetadata(DateTimeOffset.MinValue, DateTimeOffset.MinValue, [], [], [])
            : BuildFileMetadata(history);
    }
}
