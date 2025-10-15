using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace FileSearch
{
    /// <summary>
    /// Model class representing a search result item with status tracking
    /// </summary>
    public class SearchResult : INotifyPropertyChanged
    {
        private string _status = "Available";
        private bool _isMissing = false;
        
        public string FileName { get; set; }
        public string FullPath { get; set; }
        public bool IsFolder { get; set; }
        public string ParentFolder { get; set; }
        
        // Additional properties for UI display
        public string FileSize { get; set; }
        public string LastModified { get; set; }
        
        // File type property for displaying extensions
        public string FileType
        {
            get
            {
                if (IsFolder)
                    return "Folder";
                
                try
                {
                    var extension = Path.GetExtension(FullPath);
                    return string.IsNullOrEmpty(extension) ? "No Extension" : extension.ToLower();
                }
                catch
                {
                    return "Unknown";
                }
            }
        }
        
        // Status properties for missing file detection
        public string Status 
        { 
            get => _status; 
            set 
            { 
                _status = value; 
                OnPropertyChanged();
            } 
        }
        
        public bool IsMissing 
        { 
            get => _isMissing; 
            set 
            { 
                _isMissing = value; 
                Status = value ? "Missing" : "Available";
                OnPropertyChanged();
                OnPropertyChanged(nameof(Status));
            } 
        }
        
        public SearchResult()
        {
            FileName = string.Empty;
            FullPath = string.Empty;
            ParentFolder = string.Empty;
            FileSize = string.Empty;
            LastModified = string.Empty;
            Status = "Available";
            IsMissing = false;
        }
        
        /// <summary>
        /// Checks if the file or directory still exists and updates status
        /// </summary>
        public void UpdateStatus()
        {
            try
            {
                bool exists = IsFolder ? 
                    System.IO.Directory.Exists(FullPath) : 
                    System.IO.File.Exists(FullPath);
                
                IsMissing = !exists;
            }
            catch
            {
                IsMissing = true;
            }
        }
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}