using System;
using System.Collections.Generic;
using System.Linq;

namespace FileSearch
{
    /// <summary>
    /// Model class representing a saved search session with extended metadata
    /// </summary>
    public class SearchSession
    {
        public string SearchTerm { get; set; }
        public string SearchPath { get; set; }
        public DateTime SavedAt { get; set; }
        public List<SearchResult> Results { get; set; }
        
        // Extended metadata properties
        public int TotalResults { get; set; }
        public double SearchDurationSeconds { get; set; }
        public string SessionDescription { get; set; }
        public long TotalFileSize { get; set; }
        public int FileCount { get; set; }
        public int DirectoryCount { get; set; }
        
        public SearchSession()
        {
            SearchTerm = string.Empty;
            SearchPath = string.Empty;
            SavedAt = DateTime.Now;
            Results = new();
            SessionDescription = string.Empty;
        }
        
        public SearchSession(string searchTerm, string searchPath, IEnumerable<SearchResult> results, double searchDuration = 0)
        {
            SearchTerm = searchTerm;
            SearchPath = searchPath;
            SavedAt = DateTime.Now;
            Results = new(results);
            SearchDurationSeconds = searchDuration;
            
            // Calculate metadata
            TotalResults = Results.Count;
            FileCount = Results.Count(r => !r.IsFolder);
            DirectoryCount = Results.Count(r => r.IsFolder);
            
            // Calculate total file size (skip directories)
            TotalFileSize = Results.Where(r => !r.IsFolder && !string.IsNullOrEmpty(r.FileSize) && r.FileSize != "<DIR>")
                                  .Sum(r => ParseFileSize(r.FileSize));
            
            // Generate description
            SessionDescription = GenerateDescription();
        }
        
        /// <summary>
        /// Parses file size string to bytes for calculation
        /// </summary>
        private long ParseFileSize(string sizeString)
        {
            try
            {
                if (string.IsNullOrEmpty(sizeString) || sizeString == "<DIR>")
                    return 0;
                
                var parts = sizeString.Split(' ');
                if (parts.Length != 2) return 0;
                
                if (!double.TryParse(parts[0], out double size)) return 0;
                
                return parts[1].ToUpper() switch
                {
                    "B" => (long)size,
                    "KB" => (long)(size * 1024),
                    "MB" => (long)(size * 1024 * 1024),
                    "GB" => (long)(size * 1024 * 1024 * 1024),
                    "TB" => (long)(size * 1024 * 1024 * 1024 * 1024),
                    _ => 0
                };
            }
            catch
            {
                return 0;
            }
        }
        
        /// <summary>
        /// Formats total file size for display
        /// </summary>
        public static string GetFormattedTotalSize(long totalFileSize)
        {
            if (totalFileSize == 0) return "0 B";
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = totalFileSize;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
        
        /// <summary>
        /// Generates a descriptive summary of the search session
        /// </summary>
        private string GenerateDescription()
        {
            if (TotalResults == 0)
                return "No items found";
            
            var parts = new List<string>();
            
            if (FileCount > 0)
                parts.Add($"{FileCount} file{(FileCount != 1 ? "s" : "")}");
            
            if (DirectoryCount > 0)
                parts.Add($"{DirectoryCount} folder{(DirectoryCount != 1 ? "s" : "")}");
            
            var description = string.Join(" and ", parts);
            
            if (TotalFileSize > 0)
                description += $" ({GetFormattedTotalSize(TotalFileSize)})";
            
            if (SearchDurationSeconds > 0)
                description += $" in {SearchDurationSeconds:F2}s";
            
            return description;
        }
    }
}