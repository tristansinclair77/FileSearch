using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FileSearch
{
    /// <summary>
    /// Service for saving and loading search sessions to/from JSON files
    /// </summary>
    public class SearchSessionService
    {
        public SearchSessionService() { }

        private static readonly string SavedSearchesFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SavedSearches");
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>
        /// Ensures the SavedSearches folder exists
        /// </summary>
        public static void EnsureSavedSearchesFolderExists()
        {
            if (!Directory.Exists(SavedSearchesFolder))
            {
                Directory.CreateDirectory(SavedSearchesFolder);
            }
        }

        /// <summary>
        /// Saves a search session to a JSON file
        /// </summary>
        /// <param name="searchSession">The search session to save</param>
        /// <returns>The filename of the saved session</returns>
        public static string SaveSearchSession(SearchSession searchSession)
        {
            try
            {
                EnsureSavedSearchesFolderExists();

                // Generate a safe filename based on search term and timestamp
                string safeSearchTerm = GetSafeFileName(searchSession.SearchTerm);
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"{safeSearchTerm}_{timestamp}.json";
                string filePath = Path.Combine(SavedSearchesFolder, fileName);

                // Serialize and save
                string jsonString = JsonSerializer.Serialize(searchSession, JsonOptions);
                File.WriteAllText(filePath, jsonString);

                return fileName;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to save search session: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Loads a search session from a JSON file
        /// </summary>
        /// <param name="fileName">The filename to load</param>
        /// <returns>The loaded search session</returns>
        public static SearchSession LoadSearchSession(string fileName)
        {
            try
            {
                string filePath = Path.Combine(SavedSearchesFolder, fileName);
                
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"Search session file not found: {fileName}");
                }

                string jsonString = File.ReadAllText(filePath);
                var searchSession = JsonSerializer.Deserialize<SearchSession>(jsonString, JsonOptions);
                
                return searchSession ?? throw new InvalidOperationException("Failed to deserialize search session");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load search session: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets all saved search session files
        /// </summary>
        /// <returns>List of filenames</returns>
        public static List<string> GetSavedSearchFiles()
        {
            try
            {
                EnsureSavedSearchesFolderExists();

                if (!Directory.Exists(SavedSearchesFolder))
                {
                    return new List<string>();
                }

                var validFiles = new List<string>();

                return Directory.GetFiles(SavedSearchesFolder, "*.json")
                    .Select(Path.GetFileName)
                    .Where(f => !string.IsNullOrEmpty(f))
                    .OrderByDescending(f => File.GetCreationTime(Path.Combine(SavedSearchesFolder, f)))
                    .ToList();
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// Gets a display-friendly name for a saved search file
        /// </summary>
        /// <param name="fileName">The JSON filename</param>
        /// <returns>Display name</returns>
        public static string GetDisplayName(string fileName)
        {
            try
            {
                if (string.IsNullOrEmpty(fileName) || !fileName.EndsWith(".json"))
                {
                    return fileName;
                }

                // Remove .json extension and try to parse the filename
                string nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
                
                // Try to split by last underscore to separate search term from timestamp
                int lastUnderscoreIndex = nameWithoutExtension.LastIndexOf('_');
                if (lastUnderscoreIndex > 0)
                {
                    string searchTerm = nameWithoutExtension[..lastUnderscoreIndex];
                    string timestampPart = nameWithoutExtension[(lastUnderscoreIndex + 1)..];
                    
                    // Try to parse timestamp for display
                    if (DateTime.TryParseExact(timestampPart, "yyyyMMdd_HHmmss", null, 
                        System.Globalization.DateTimeStyles.None, out DateTime timestamp))
                    {
                        return $"{searchTerm} ({timestamp:yyyy-MM-dd HH:mm})";
                    }
                }

                return nameWithoutExtension;
            }
            catch (Exception)
            {
                return fileName;
            }
        }

        /// <summary>
        /// Deletes a saved search session file
        /// </summary>
        /// <param name="fileName">The filename to delete</param>
        public static void DeleteSearchSession(string fileName)
        {
            try
            {
                string filePath = Path.Combine(SavedSearchesFolder, fileName);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to delete search session: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Creates a safe filename from a search term
        /// </summary>
        private static string GetSafeFileName(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return "search";
            }

            // Remove or replace invalid filename characters
            char[] invalidChars = Path.GetInvalidFileNameChars();
            string safeName = searchTerm;
            foreach (char invalidChar in invalidChars)
            {
                safeName = safeName.Replace(invalidChar, '_');
            }

            // Limit length and remove extra spaces
            safeName = safeName.Trim().Replace(" ", "_");
            if (safeName.Length > 50)
            {
                safeName = safeName[..50];
            }

            return string.IsNullOrWhiteSpace(safeName) ? "search" : safeName;
        }
    }
}