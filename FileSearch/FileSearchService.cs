using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace FileSearch
{
    /// <summary>
    /// Service class for safely searching files and directories recursively with cancellation support
    /// </summary>
    public class FileSearchService
    {
        /// <summary>
        /// Asynchronously searches for files and directories matching the specified pattern with cancellation support and max depth
        /// </summary>
        /// <param name="rootPath">The root directory to start searching from</param>
        /// <param name="searchPattern">The search pattern (e.g., "*.txt", "*partial*", etc.)</param>
        /// <param name="progressCallback">Optional callback to report progress during search</param>
        /// <param name="cancellationToken">Token to cancel the search operation</param>
        /// <param name="maxDepth">Maximum depth to traverse (1-10). 1 means only direct children of root, 2 means children and grandchildren, etc.</param>
        /// <returns>Collection of SearchResult objects</returns>
        public async Task<IEnumerable<SearchResult>> SearchRecursivelyAsync(
            string rootPath,
            string searchPattern = "*",
            IProgress<SearchProgress> progressCallback = null,
            CancellationToken cancellationToken = default,
            int maxDepth = int.MaxValue)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            {
                return Enumerable.Empty<SearchResult>();
            }

            // Clamp depth to a reasonable range if specified
            if (maxDepth < 1) maxDepth = 1;
            if (maxDepth > 10) maxDepth = 10;

            var results = new List<SearchResult>();

            try
            {
                progressCallback?.Report(new SearchProgress { Status = "Searching...", CurrentPath = rootPath });

                await Task.Run(() =>
                {
                    // BFS traversal up to maxDepth levels from root
                    // Depth 1 = only search the root directory itself
                    // Depth 2 = search root + 1 level deeper, etc.
                    var queue = new Queue<(string Path, int Depth)>();
                    queue.Enqueue((rootPath, 1)); // Start at depth 1 (the selected folder)

                    int filesFound = 0;
                    int dirsFound = 0;

                    while (queue.Count > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var (currentPath, depth) = queue.Dequeue();

                        try
                        {
                            // Enumerate files in the current directory (TopDirectoryOnly)
                            IEnumerable<string> files = Array.Empty<string>();
                            try
                            {
                                files = Directory.EnumerateFiles(currentPath, searchPattern, SearchOption.TopDirectoryOnly);
                            }
                            catch (UnauthorizedAccessException)
                            {
                                progressCallback?.Report(new SearchProgress { Status = $"Access denied to: {currentPath}", IsError = false });
                            }
                            catch (PathTooLongException)
                            {
                                progressCallback?.Report(new SearchProgress { Status = $"Path too long: {currentPath}", IsError = false });
                            }
                            catch (IOException)
                            {
                                progressCallback?.Report(new SearchProgress { Status = $"I/O error in: {currentPath}", IsError = false });
                            }

                            foreach (var filePath in files)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                var result = CreateFileResult(filePath);
                                if (result != null)
                                {
                                    results.Add(result);
                                    filesFound++;
                                    if (filesFound % 100 == 0)
                                    {
                                        progressCallback?.Report(new SearchProgress
                                        {
                                            Status = $"Found {filesFound} files...",
                                            CurrentPath = filePath,
                                            ItemsFound = filesFound
                                        });
                                    }
                                }
                            }

                            // Enumerate directories in the current directory (TopDirectoryOnly)
                            IEnumerable<string> childDirs = Array.Empty<string>();
                            try
                            {
                                childDirs = Directory.EnumerateDirectories(currentPath, "*", SearchOption.TopDirectoryOnly);
                            }
                            catch (UnauthorizedAccessException)
                            {
                                progressCallback?.Report(new SearchProgress { Status = $"Access denied to: {currentPath}", IsError = false });
                            }
                            catch (PathTooLongException)
                            {
                                progressCallback?.Report(new SearchProgress { Status = $"Path too long: {currentPath}", IsError = false });
                            }
                            catch (IOException)
                            {
                                progressCallback?.Report(new SearchProgress { Status = $"I/O error in: {currentPath}", IsError = false });
                            }

                            foreach (var dirPath in childDirs)
                            {
                                cancellationToken.ThrowIfCancellationRequested();

                                // Add directory to results if it matches the pattern
                                try
                                {
                                    var dirName = new DirectoryInfo(dirPath).Name;
                                    if (MatchesPattern(dirName, searchPattern))
                                    {
                                        var dirResult = CreateDirectoryResult(dirPath);
                                        if (dirResult != null)
                                        {
                                            results.Add(dirResult);
                                            dirsFound++;
                                            if (dirsFound % 50 == 0)
                                            {
                                                progressCallback?.Report(new SearchProgress
                                                {
                                                    Status = $"Found {dirsFound} directories...",
                                                    CurrentPath = dirPath,
                                                    ItemsFound = dirsFound
                                                });
                                            }
                                        }
                                    }
                                }
                                catch (Exception)
                                {
                                    // Ignore errors creating directory result
                                }

                                // Traverse deeper only if we haven't reached the maximum depth
                                // depth < maxDepth means we can go one level deeper
                                if (depth < maxDepth)
                                {
                                    queue.Enqueue((dirPath, depth + 1));
                                }
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                    }
                }, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Log other exceptions but continue
                progressCallback?.Report(new SearchProgress { Status = $"Error during search: {ex.Message}", IsError = true });
            }

            return results;
        }

        // Simple wildcard pattern matcher (supports * and ?)
        private static bool MatchesPattern(string input, string pattern)
        {
            if (string.IsNullOrEmpty(pattern) || pattern == "*") return true;
            // Escape regex special chars, then replace wildcard tokens
            string regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            return Regex.IsMatch(input, regexPattern, RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Legacy synchronous method for backward compatibility
        /// </summary>
        public IEnumerable<SearchResult> SearchRecursively(string rootPath, string searchPattern = "*", int maxDepth = int.MaxValue)
        {
            return SearchRecursivelyAsync(rootPath, searchPattern, null, default, maxDepth).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Creates a SearchResult for a file with safe exception handling
        /// </summary>
        public static SearchResult CreateFileResult(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                return new SearchResult
                {
                    FileName = fileInfo.Name,
                    FullPath = fileInfo.FullName,
                    IsFolder = false,
                    ParentFolder = fileInfo.DirectoryName ?? string.Empty,
                    FileSize = FormatFileSize(fileInfo.Length),
                    LastModified = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
                };
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (PathTooLongException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
        }

        /// <summary>
        /// Creates a SearchResult for a directory with safe exception handling
        /// </summary>
        public static SearchResult CreateDirectoryResult(string dirPath)
        {
            try
            {
                var dirInfo = new DirectoryInfo(dirPath);
                return new SearchResult
                {
                    FileName = dirInfo.Name,
                    FullPath = dirInfo.FullName,
                    IsFolder = true,
                    ParentFolder = dirInfo.Parent?.FullName ?? string.Empty,
                    FileSize = "<DIR>",
                    LastModified = dirInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
                };
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }
            catch (PathTooLongException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
        }

        /// <summary>
        /// Formats file size in human-readable format
        /// </summary>
        public static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        /// <summary>
        /// Asynchronously searches for items that contain the specified text in their names with cancellation support and max depth
        /// </summary>
        /// <param name="rootPath">The root directory to search</param>
        /// <param name="searchText">Text to search for in file/folder names</param>
        /// <param name="progressCallback">Optional callback to report progress during search</param>
        /// <param name="cancellationToken">Token to cancel the search operation</param>
        /// <param name="maxDepth">Maximum depth to traverse</param>
        /// <returns>Collection of matching SearchResult objects</returns>
        public async Task<IEnumerable<SearchResult>> SearchByNameAsync(
            string rootPath,
            string searchText,
            IProgress<SearchProgress> progressCallback = null,
            CancellationToken cancellationToken = default,
            int maxDepth = int.MaxValue)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return await SearchRecursivelyAsync(rootPath, "*", progressCallback, cancellationToken, maxDepth);
            }

            return await SearchRecursivelyAsync(rootPath, $"*{searchText}*", progressCallback, cancellationToken, maxDepth);
        }

        /// <summary>
        /// Legacy synchronous method for backward compatibility
        /// </summary>
        public IEnumerable<SearchResult> SearchByName(string rootPath, string searchText, int maxDepth = int.MaxValue)
        {
            return SearchByNameAsync(rootPath, searchText, null, default, maxDepth).GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Progress information for search operations
    /// </summary>
    public class SearchProgress
    {
        public string Status { get; set; } = string.Empty;
        public string CurrentPath { get; set; } = string.Empty;
        public int ItemsFound { get; set; } = 0;
        public bool IsError { get; set; } = false;
    }
}