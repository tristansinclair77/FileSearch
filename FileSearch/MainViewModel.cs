using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FileSearch
{
    /// <summary>
    /// Main ViewModel for the FileSearch application implementing MVVM pattern
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly SearchService _searchService;
        private CancellationTokenSource _searchCancellationTokenSource;
        
        // Private fields for properties
        private string _searchText = string.Empty;
        private string _selectedFolderPath = string.Empty;
        private string _statusText = "Ready";
        private bool _isSearching = false;
        private Visibility _progressBarVisibility = Visibility.Collapsed;
        private Visibility _metadataPanelVisibility = Visibility.Collapsed;
        private ComboBoxItem _selectedSavedSearch;
        private int _maxDepth = 1; // Default to 1 as requested
        
        // Metadata display properties
        private string _metadataSearchTerm = string.Empty;
        private string _metadataSearchPath = string.Empty;
        private string _metadataTimestamp = string.Empty;
        private string _metadataTotalResults = string.Empty;
        private string _metadataFileStats = string.Empty;
        private string _metadataSearchDuration = string.Empty;
        private string _sessionHeaderText = "Loaded Search Session";
        private Brush _sessionHeaderForeground = new SolidColorBrush(Color.FromRgb(39, 174, 96));

        public MainViewModel()
        {
            _searchService = new SearchService();
            SearchResults = new();
            SavedSearches = new();
            
            InitializeCommands();
            LoadSavedSearchesList();
        }

        #region Properties

        public ObservableCollection<SearchResult> SearchResults { get; }
        public ObservableCollection<ComboBoxItem> SavedSearches { get; }

        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
            }
        }

        public int MaxDepth
        {
            get => _maxDepth;
            set
            {
                // Clamp 1..10
                var newVal = value;
                if (newVal < 1) newVal = 1;
                if (newVal > 10) newVal = 10;
                _maxDepth = newVal;
                OnPropertyChanged();
            }
        }

        public string SelectedFolderPath
        {
            get => _selectedFolderPath;
            set
            {
                _selectedFolderPath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedFolderDisplayText));
            }
        }

        public string SelectedFolderDisplayText
        {
            get
            {
                if (string.IsNullOrEmpty(_selectedFolderPath))
                    return "No folder selected";
                return _selectedFolderPath;
            }
        }

        public string StatusText
        {
            get => _statusText;
            set
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }

        public bool IsSearching
        {
            get => _isSearching;
            set
            {
                _isSearching = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNotSearching));
                OnPropertyChanged(nameof(SearchButtonContent));
            }
        }

        public bool IsNotSearching => !IsSearching;

        public string SearchButtonContent => IsSearching ? "?? Searching..." : "?? Search";

        public Visibility ProgressBarVisibility
        {
            get => _progressBarVisibility;
            set
            {
                _progressBarVisibility = value;
                OnPropertyChanged();
            }
        }

        public Visibility MetadataPanelVisibility
        {
            get => _metadataPanelVisibility;
            set
            {
                _metadataPanelVisibility = value;
                OnPropertyChanged();
            }
        }

        public ComboBoxItem SelectedSavedSearch
        {
            get => _selectedSavedSearch;
            set
            {
                _selectedSavedSearch = value;
                OnPropertyChanged();
            }
        }

        // Metadata display properties
        public string MetadataSearchTerm
        {
            get => _metadataSearchTerm;
            set
            {
                _metadataSearchTerm = value;
                OnPropertyChanged();
            }
        }

        public string MetadataSearchPath
        {
            get => _metadataSearchPath;
            set
            {
                _metadataSearchPath = value;
                OnPropertyChanged();
            }
        }

        public string MetadataTimestamp
        {
            get => _metadataTimestamp;
            set
            {
                _metadataTimestamp = value;
                OnPropertyChanged();
            }
        }

        public string MetadataTotalResults
        {
            get => _metadataTotalResults;
            set
            {
                _metadataTotalResults = value;
                OnPropertyChanged();
            }
        }

        public string MetadataFileStats
        {
            get => _metadataFileStats;
            set
            {
                _metadataFileStats = value;
                OnPropertyChanged();
            }
        }

        public string MetadataSearchDuration
        {
            get => _metadataSearchDuration;
            set
            {
                _metadataSearchDuration = value;
                OnPropertyChanged();
            }
        }

        public string SessionHeaderText
        {
            get => _sessionHeaderText;
            set
            {
                _sessionHeaderText = value;
                OnPropertyChanged();
            }
        }

        public Brush SessionHeaderForeground
        {
            get => _sessionHeaderForeground;
            set
            {
                _sessionHeaderForeground = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region Commands

        public ICommand SelectFolderCommand { get; private set; }
        public ICommand SearchCommand { get; private set; }
        public ICommand StopCommand { get; private set; } // New Stop command
        public ICommand SaveSessionCommand { get; private set; }
        public ICommand LoadSessionCommand { get; private set; }
        public ICommand DeleteSavedSearchCommand { get; private set; }
        public ICommand RefreshSavedSearchesCommand { get; private set; }
        public ICommand OpenFolderCommand { get; private set; }
        public ICommand OpenFileCommand { get; private set; }
        public ICommand DeleteFileCommand { get; private set; }

        private void InitializeCommands()
        {
            SelectFolderCommand = new RelayCommand(_ => SelectFolder());
            SearchCommand = new RelayCommand(async _ => await SearchAsync(), _ => !IsSearching);
            StopCommand = new RelayCommand(_ => StopSearch(), _ => IsSearching); // New Stop command
            SaveSessionCommand = new RelayCommand(_ => SaveSession(), _ => !IsSearching && SearchResults.Count > 0);
            LoadSessionCommand = new RelayCommand(_ => LoadSession(), _ => !IsSearching && SelectedSavedSearch != null);
            DeleteSavedSearchCommand = new RelayCommand(_ => DeleteSavedSearch(), _ => SelectedSavedSearch != null);
            RefreshSavedSearchesCommand = new RelayCommand(_ => LoadSavedSearchesList());
            OpenFolderCommand = new RelayCommand<SearchResult>(OpenFolder, result => result != null && !result.IsMissing);
            OpenFileCommand = new RelayCommand<SearchResult>(OpenFile, result => result != null && !result.IsMissing);
            DeleteFileCommand = new RelayCommand<SearchResult>(DeleteFile, result => result != null && !result.IsMissing);
        }

        #endregion

        #region Command Implementations

        private void SelectFolder()
        {
            try
            {
                var folderPath = _searchService.SelectFolder();
                if (!string.IsNullOrEmpty(folderPath))
                {
                    SelectedFolderPath = folderPath;
                    // Don't paste into search box as per requirement #3
                    StatusText = $"Selected folder: {folderPath}";
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error selecting folder: {ex.Message}";
            }
        }

        private void StopSearch()
        {
            try
            {
                if (_searchCancellationTokenSource != null && !_searchCancellationTokenSource.IsCancellationRequested)
                {
                    _searchCancellationTokenSource.Cancel();
                    StatusText = "Stopping search...";
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error stopping search: {ex.Message}";
            }
        }

        private async Task SearchAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                if (string.IsNullOrEmpty(SearchText))
                {
                    MessageBox.Show("Please enter a search path or pattern.", "Search Path Required", 
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Cancel any existing search
                if (_searchCancellationTokenSource != null)
                {
                    _searchCancellationTokenSource.Cancel();
                    _searchCancellationTokenSource.Dispose();
                }

                // Create new cancellation token for this search
                _searchCancellationTokenSource = new CancellationTokenSource();
                var cancellationToken = _searchCancellationTokenSource.Token;

                // Clear previous results and hide metadata panel
                Application.Current.Dispatcher.Invoke(() =>
                {
                    SearchResults.Clear();
                    HideSearchSessionMetadata();
                });
                
                StatusText = $"Starting search with depth {MaxDepth}...";
                SetSearchInProgress(true);

                // Create progress reporter
                var progress = new Progress<SearchProgress>(progressInfo =>
                {
                    // Update UI on the UI thread
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        StatusText = progressInfo.Status;
                        
                        if (progressInfo.IsError)
                        {
                            StatusText = $"Warning: {progressInfo.Status}";
                        }
                    });
                });

                try
                {
                    // Perform async search with proper MaxDepth parameter
                    var results = await _searchService.SearchAsync(SearchText, SelectedFolderPath, MaxDepth, cancellationToken, progress);
                    
                    // Check if operation was cancelled
                    if (cancellationToken.IsCancellationRequested)
                    {
                        StatusText = "Search cancelled";
                        return;
                    }

                    // Add results to ObservableCollection on UI thread
                    var resultsList = results.ToList();
                    StatusText = $"Adding {resultsList.Count} results...";
                    
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        foreach (var result in resultsList)
                        {
                            // Check for cancellation during UI updates
                            if (cancellationToken.IsCancellationRequested)
                            {
                                StatusText = "Search cancelled during result loading";
                                return;
                            }
                            
                            // Set initial status for new search results
                            result.Status = "Available";
                            result.IsMissing = false;
                            
                            SearchResults.Add(result);
                        }
                    });

                    stopwatch.Stop();
                    StatusText = $"Found {SearchResults.Count} results in {stopwatch.Elapsed.TotalSeconds:F2} seconds (depth: {MaxDepth})";
                }
                catch (OperationCanceledException)
                {
                    stopwatch.Stop();
                    StatusText = $"Search cancelled after {stopwatch.Elapsed.TotalSeconds:F2} seconds";
                }
                catch (UnauthorizedAccessException ex)
                {
                    stopwatch.Stop();
                    StatusText = $"Access denied: {ex.Message}";
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    StatusText = $"Search error: {ex.Message}";
                    if (ex.Message.Contains("Please select a valid folder"))
                    {
                        MessageBox.Show(ex.Message, "Invalid Path", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                StatusText = $"Unexpected error: {ex.Message}";
            }
            finally
            {
                SetSearchInProgress(false);
                
                // Clean up cancellation token
                if (_searchCancellationTokenSource != null)
                {
                    _searchCancellationTokenSource.Dispose();
                    _searchCancellationTokenSource = null;
                }
            }
        }

        private void SaveSession()
        {
            try
            {
                if (string.IsNullOrEmpty(SearchText))
                {
                    MessageBox.Show("Please enter a search term before saving.", "Nothing to Save", 
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (SearchResults.Count == 0)
                {
                    MessageBox.Show("No search results to save. Please perform a search first.", "No Results", 
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Calculate search duration from status text if available
                double searchDuration = ExtractSearchDurationFromStatus();

                // Save search session
                string fileName = _searchService.SaveSearchSession(SearchText, SelectedFolderPath, SearchResults, searchDuration);
                
                // Refresh the saved searches list
                LoadSavedSearchesList();
                
                StatusText = $"Search session saved as '{fileName}' with {SearchResults.Count} results";
                
                // Calculate session details for display
                var fileCount = SearchResults.Count(r => !r.IsFolder);
                var directoryCount = SearchResults.Count(r => r.IsFolder);
                
                MessageBox.Show($"Search session saved successfully as '{fileName}'.\n\nSession Details:\n• {SearchResults.Count} total results\n• {fileCount} files, {directoryCount} folders", 
                              "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusText = $"Error saving search session: {ex.Message}";
            }
        }

        private void LoadSession()
        {
            try
            {
                if (SelectedSavedSearch?.Tag?.ToString() is not string fileName)
                {
                    MessageBox.Show("Please select a saved search from the dropdown.", "No Selection", 
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Load search session
                var searchSession = _searchService.LoadSearchSession(fileName);
                
                // Update UI with loaded data
                SearchText = searchSession.SearchTerm;
                SelectedFolderPath = searchSession.SearchPath;
                
                // Clear and populate search results
                SearchResults.Clear();
                
                int missingCount = SearchService.UpdateSearchResultsStatus(searchSession.Results);
                
                foreach (var result in searchSession.Results)
                {
                    SearchResults.Add(result);
                }
                
                // Display metadata above DataGrid
                DisplaySearchSessionMetadata(searchSession, missingCount);
                
                StatusText = $"Loaded {SearchResults.Count} results from {searchSession.SavedAt:yyyy-MM-dd HH:mm} ({missingCount} missing)";
                
                string missingInfo = missingCount > 0 ? $"\n?? {missingCount} items are no longer available" : "";
                MessageBox.Show($"Search session loaded successfully.\n\nLoaded: {SearchResults.Count} items from {searchSession.SavedAt:yyyy-MM-dd HH:mm}{missingInfo}", 
                              "Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusText = $"Error loading search session: {ex.Message}";
                HideSearchSessionMetadata();
            }
        }

        private void DeleteSavedSearch()
        {
            try
            {
                if (SelectedSavedSearch?.Tag?.ToString() is not string fileName ||
                    SelectedSavedSearch?.Content?.ToString() is not string displayName)
                {
                    MessageBox.Show("Please select a saved search to delete.", "No Selection", 
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show(
                    $"Are you sure you want to delete the saved search '{displayName}'?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _searchService.DeleteSearchSession(fileName);
                    LoadSavedSearchesList();
                    StatusText = "Saved search deleted successfully";
                    MessageBox.Show("Saved search deleted successfully.", "Deleted", 
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error deleting saved search: {ex.Message}";
            }
        }

        private void OpenFolder(SearchResult searchResult)
        {
            try
            {
                _searchService.OpenFolderAndHighlight(searchResult);
                StatusText = $"Opened folder for: {searchResult.FileName}";
            }
            catch (Exception ex)
            {
                StatusText = $"Error opening folder: {ex.Message}";
                if (ex is FileNotFoundException)
                {
                    MessageBox.Show(ex.Message, "File Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void OpenFile(SearchResult searchResult)
        {
            try
            {
                _searchService.OpenFileOrFolder(searchResult);
                StatusText = $"Opened: {searchResult.FileName}";
            }
            catch (Exception ex)
            {
                StatusText = $"Error opening file: {ex.Message}";
                if (ex is FileNotFoundException)
                {
                    MessageBox.Show(ex.Message, "File Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void DeleteFile(SearchResult searchResult)
        {
            try
            {
                var confirmResult = MessageBox.Show(
                    $"Are you sure you want to send '{searchResult.FileName}' to the recycle bin?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirmResult == MessageBoxResult.Yes)
                {
                    _searchService.DeleteFileOrFolder(searchResult);
                    
                    // Remove from results collection after successful deletion
                    SearchResults.Remove(searchResult);
                    
                    StatusText = $"Deleted: {searchResult.FileName}";
                    MessageBox.Show($"'{searchResult.FileName}' has been sent to the recycle bin successfully.", 
                                  "Deleted", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error deleting item: {ex.Message}";
                if (ex is FileNotFoundException)
                {
                    MessageBox.Show(ex.Message, "File Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    // Remove from results since it no longer exists
                    SearchResults.Remove(searchResult);
                }
            }
        }

        #endregion

        #region Helper Methods

        private void SetSearchInProgress(bool inProgress)
        {
            IsSearching = inProgress;
            ProgressBarVisibility = inProgress ? Visibility.Visible : Visibility.Collapsed;
        }

        private void LoadSavedSearchesList()
        {
            try
            {
                SavedSearches.Clear();
                var savedFiles = _searchService.GetSavedSearchFiles();
                
                foreach (var fileName in savedFiles)
                {
                    var displayName = SearchService.GetDisplayName(fileName);
                    SavedSearches.Add(new ComboBoxItem
                    {
                        Content = displayName,
                        Tag = fileName // Store the actual filename in the Tag
                    });
                }
                
                StatusText = $"Loaded {savedFiles.Count} saved searches";
            }
            catch (Exception ex)
            {
                StatusText = $"Error loading saved searches: {ex.Message}";
            }
        }

        private void DisplaySearchSessionMetadata(SearchSession session, int missingCount)
        {
            try
            {
                // Update metadata display
                MetadataSearchTerm = $"?? Search Term: {session.SearchTerm}";
                MetadataSearchPath = $"?? Root Path: {(string.IsNullOrEmpty(session.SearchPath) ? "Not specified" : session.SearchPath)}";
                MetadataTimestamp = $"? Saved: {session.SavedAt:yyyy-MM-dd HH:mm:ss}";
                
                MetadataTotalResults = $"?? Total Results: {session.TotalResults}";
                MetadataFileStats = $"?? Files: {session.FileCount} | ?? Folders: {session.DirectoryCount}";
                
                if (session.SearchDurationSeconds > 0)
                    MetadataSearchDuration = $"?? Search Duration: {session.SearchDurationSeconds:F2} seconds";
                else
                    MetadataSearchDuration = "?? Search Duration: Not recorded";
                
                // Update header with missing file info
                if (missingCount > 0)
                {
                    SessionHeaderText = $"Loaded Search Session (?? {missingCount} items missing)";
                    SessionHeaderForeground = new SolidColorBrush(Colors.Orange);
                }
                else
                {
                    SessionHeaderText = "Loaded Search Session";
                    SessionHeaderForeground = new SolidColorBrush(Color.FromRgb(39, 174, 96)); // #27AE60
                }
                
                // Show metadata panel
                MetadataPanelVisibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                StatusText = $"Error displaying metadata: {ex.Message}";
            }
        }

        private void HideSearchSessionMetadata()
        {
            MetadataPanelVisibility = Visibility.Collapsed;
        }

        private double ExtractSearchDurationFromStatus()
        {
            try
            {
                if (StatusText.Contains("in ") && StatusText.Contains(" seconds"))
                {
                    var startIndex = StatusText.IndexOf("in ") + 3;
                    var endIndex = StatusText.IndexOf(" seconds");
                    if (startIndex > 2 && endIndex > startIndex)
                    {
                        var durationText = StatusText[startIndex..endIndex];
                        if (double.TryParse(durationText, out double duration))
                            return duration;
                    }
                }
            }
            catch
            {
                // Ignore parsing errors
            }
            return 0;
        }

        /// <summary>
        /// Cleanup when the ViewModel is disposed
        /// </summary>
        public void Cleanup()
        {
            // Cancel any ongoing search
            if (_searchCancellationTokenSource != null)
            {
                _searchCancellationTokenSource.Cancel();
                _searchCancellationTokenSource.Dispose();
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}