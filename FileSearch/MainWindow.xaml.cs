using System.Windows;

namespace FileSearch
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            
            // Get the ViewModel from DataContext
            _viewModel = DataContext as MainViewModel;
        }

        /// <summary>
        /// Cleanup on window closing
        /// </summary>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Cleanup ViewModel resources
            _viewModel?.Cleanup();
            
            base.OnClosing(e);
        }
    }
}