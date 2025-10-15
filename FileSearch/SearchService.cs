using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAPICodePack.Dialogs;
using Microsoft.VisualBasic.FileIO;

namespace FileSearch
{
    /// <summary>
    /// Service class that encapsulates all file/folder operations for MVVM pattern
    /// </summary>
    public class SearchService
    {
        private readonly FileSearchService _fileSearchService;

        public SearchService()
        {
            _fileSearchService = new FileSearchService();
        }

        /// <summary>
        /// Performs an async search operation
        /// </summary>
        public async Task<IEnumerable<SearchResult>> SearchAsync(string searchPath, string selectedFolderPath, int maxDepth,
            CancellationToken cancellationToken, IProgress<SearchProgress> progress)
        {
            if (string.IsNullOrEmpty(searchPath))
                throw new ArgumentException("Search path cannot be empty", nameof(searchPath));

            // Clamp depth between 1 and 10
            if (maxDepth < 1) maxDepth = 1;
            if (maxDepth > 10) maxDepth = 10;

            // Determine search strategy and perform async search
            if (Directory.Exists(searchPath))
            {
                // It's a directory path, search all files
                return await _fileSearchService.SearchRecursivelyAsync(searchPath, "*", progress, cancellationToken, maxDepth);
            }
            else if (!string.IsNullOrEmpty(selectedFolderPath) && Directory.Exists(selectedFolderPath))
            {
                // Use selected folder with search pattern
                return await _fileSearchService.SearchByNameAsync(selectedFolderPath, searchPath, progress, cancellationToken, maxDepth);
            }
            else
            {
                // Try to extract directory and pattern from the path
                string directory = Path.GetDirectoryName(searchPath);
                string pattern = Path.GetFileName(searchPath);
                
                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                {
                    return await _fileSearchService.SearchByNameAsync(directory, pattern, progress, cancellationToken, maxDepth);
                }
                else
                {
                    throw new DirectoryNotFoundException("Please select a valid folder first or enter a valid path.");
                }
            }
        }

        /// <summary>
        /// Opens a folder dialog and returns the selected folder path
        /// </summary>
        public string SelectFolder()
        {
            var dialog = new CommonOpenFileDialog
            {
                Title = "Select Folder to Search",
                IsFolderPicker = true,
                EnsurePathExists = true
            };

            return dialog.ShowDialog() == CommonFileDialogResult.Ok ? dialog.FileName : null;
        }

        /// <summary>
        /// Opens the parent folder and highlights the specified file/folder
        /// </summary>
        public void OpenFolderAndHighlight(SearchResult searchResult)
        {
            if (searchResult == null || string.IsNullOrEmpty(searchResult.FullPath))
                throw new ArgumentException("Invalid search result", nameof(searchResult));

            if (searchResult.IsMissing)
                throw new FileNotFoundException($"The item '{searchResult.FileName}' no longer exists");

            Process.Start("explorer.exe", $"/select,\"{searchResult.FullPath}\"");
        }

        /// <summary>
        /// Opens a file or folder directly with the default application
        /// </summary>
        public void OpenFileOrFolder(SearchResult searchResult)
        {
            if (searchResult == null || string.IsNullOrEmpty(searchResult.FullPath))
                throw new ArgumentException("Invalid search result", nameof(searchResult));

            if (searchResult.IsMissing)
                throw new FileNotFoundException($"The item '{searchResult.FileName}' no longer exists");

            // Check if file or folder exists before attempting to open
            if (!File.Exists(searchResult.FullPath) && !Directory.Exists(searchResult.FullPath))
            {
                throw new FileNotFoundException($"The item '{searchResult.FileName}' no longer exists at the specified location");
            }

            // Open the file or folder directly using Process.Start with UseShellExecute
            Process.Start(new ProcessStartInfo(searchResult.FullPath) { UseShellExecute = true });
        }

        /// <summary>
        /// Deletes a file or folder by sending it to the recycle bin
        /// </summary>
        public void DeleteFileOrFolder(SearchResult searchResult)
        {
            if (searchResult == null || string.IsNullOrEmpty(searchResult.FullPath))
                throw new ArgumentException("Invalid search result", nameof(searchResult));

            if (searchResult.IsMissing)
                throw new FileNotFoundException($"The item '{searchResult.FileName}' no longer exists");

            // Check if file or folder exists before attempting to delete
            if (!File.Exists(searchResult.FullPath) && !Directory.Exists(searchResult.FullPath))
            {
                throw new FileNotFoundException($"The item '{searchResult.FileName}' no longer exists at the specified location");
            }

            // Delete file or folder using Microsoft.VisualBasic.FileIO.FileSystem
            if (searchResult.IsFolder)
            {
                FileSystem.DeleteDirectory(searchResult.FullPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            }
            else
            {
                FileSystem.DeleteFile(searchResult.FullPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            }
        }

        /// <summary>
        /// Saves a search session to JSON file
        /// </summary>
        public string SaveSearchSession(string searchTerm, string searchPath, IEnumerable<SearchResult> results, double searchDuration)
        {
            if (string.IsNullOrEmpty(searchTerm))
                throw new ArgumentException("Search term cannot be empty", nameof(searchTerm));

            var searchSession = new SearchSession(searchTerm, searchPath ?? string.Empty, results, searchDuration);
            return SearchSessionService.SaveSearchSession(searchSession);
        }

        /// <summary>
        /// Loads a search session from JSON file
        /// </summary>
        public SearchSession LoadSearchSession(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentException("File name cannot be empty", nameof(fileName));

            return SearchSessionService.LoadSearchSession(fileName);
        }

        /// <summary>
        /// Gets all saved search session files
        /// </summary>
        public List<string> GetSavedSearchFiles()
        {
            return SearchSessionService.GetSavedSearchFiles();
        }

        /// <summary>
        /// Gets display name for a saved search file
        /// </summary>
        public static string GetDisplayName(string fileName)
        {
            return SearchSessionService.GetDisplayName(fileName);
        }

        /// <summary>
        /// Deletes a saved search session file
        /// </summary>
        public void DeleteSearchSession(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentException("File name cannot be empty", nameof(fileName));

            SearchSessionService.DeleteSearchSession(fileName);
        }

        /// <summary>
        /// Updates the status of all search results to check if they still exist
        /// </summary>
        public static int UpdateSearchResultsStatus(IEnumerable<SearchResult> results)
        {
            int missingCount = 0;
            foreach (var result in results)
            {
                result.UpdateStatus();
                if (result.IsMissing)
                    missingCount++;
            }
            return missingCount;
        }
    }
}