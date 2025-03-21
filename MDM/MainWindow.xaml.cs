using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Text.Json;
using System.ComponentModel;
using System.Windows.Threading;
using System.Windows.Input;
using System.Globalization;
using System.Diagnostics;
using System.Windows.Forms; // For NotifyIcon
using System.Drawing;
using System.Net.Http; // For Icon

namespace MDM
{
    public partial class MainWindow : Window
    {

        private ObservableCollection<Download> downloads;
        private DownloadManager downloadManager;
        private string downloadSubPath;
        private int maxSimultaneousDownloads;
        private static readonly string SETTINGS_FILE = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MDM", "settings.json");
        private static readonly string DOWNLOADS_FILE = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MDM", "downloads.json");
        private bool deleteFileOnRemove = true;
        private DispatcherTimer clipboardTimer;
        private NotifyIcon trayIcon;
        private bool showDownloadNotifications;
        private string theme;
        private string language;
        private string lastClipboardUrl = ""; // To avoid repeated pasting
        private static readonly HttpClient httpClient = new HttpClient(); // Shared HttpClient instance

        public ICommand DeleteSelectedDownloadsCommand { get; }
        public ICommand CopyUrlCommand { get; }
        public ICommand ShowPropertiesCommand { get; }
        public ICommand OpenFileCommand { get; }

        // Property to get the full download path dynamically
        private string DownloadPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), downloadSubPath);

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MDM");
            if (!Directory.Exists(appDataFolder))
            {
                Directory.CreateDirectory(appDataFolder);
            }

            clipboardTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            clipboardTimer.Tick += ClipboardTimer_Tick;
            clipboardTimer.Start();


            downloads = new ObservableCollection<Download>();
            downloadListView.ItemsSource = downloads;
            downloadManager = new DownloadManager();

            DeleteSelectedDownloadsCommand = new RelayCommand(() => DeleteSelectedDownloads_Click(null, null));
            CopyUrlCommand = new RelayCommand<string>(url => System.Windows.Clipboard.SetText(url));
            ShowPropertiesCommand = new RelayCommand<Download>(download => new PropertiesWindow(download).ShowDialog());
            OpenFileCommand = new RelayCommand<Download>(download =>
            {
                if (download.Status == "Completed")
                {
                    string fullPath = download.FullPath;
                    if (File.Exists(fullPath))
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = fullPath,
                                UseShellExecute = true
                            });
                        }
                        catch (Exception ex)
                        {
                            System.Windows.MessageBox.Show($"Error opening file: {ex.Message}");
                        }
                    }
                }
            }, download => download != null && download.Status == "Completed");

            // Default to a relative subpath
            downloadSubPath = "Downloads\\MDM";
            string fullDownloadPath = DownloadPath;
            if (!Directory.Exists(fullDownloadPath))
            {
                Directory.CreateDirectory(fullDownloadPath);
            }

            // Tray icon setup
            trayIcon = new NotifyIcon
            {
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location),
                Visible = true,
                Text = "Modern Download Manager"
            };
            trayIcon.DoubleClick += (s, e) => { Show(); WindowState = WindowState.Normal; };
            trayIcon.ContextMenuStrip = CreateTrayContextMenu();

            LoadSettings();
            LoadDownloads();

            foreach (var download in downloads)
            {
                download.PropertyChanged += Download_PropertyChanged;
            }
            httpClient.Timeout = TimeSpan.FromSeconds(5); // Set a reasonable timeout for HEAD requests
        }

        private ContextMenuStrip CreateTrayContextMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Maximize MDM", null, (s, e) => { Show(); WindowState = WindowState.Normal; });
            menu.Items.Add("Resume All Downloads", null, (s, e) => ResumeAllDownloads_Click(null, null));
            menu.Items.Add("Pause All Downloads", null, (s, e) => PauseAllDownloads_Click(null, null));
            menu.Items.Add("Exit", null, (s, e) => Close());
            return menu;
        }

        private void Download_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Download.Status) && sender is Download download)
            {
                if (download.Status == "Completed" && showDownloadNotifications)
                {
                    trayIcon.BalloonTipTitle = "Download Complete";
                    trayIcon.BalloonTipText = $"File: {download.FileName}";
                    trayIcon.BalloonTipIcon = ToolTipIcon.Info;
                    trayIcon.ShowBalloonTip(3000);
                }
            }
        }

        private async void ClipboardTimer_Tick(object sender, EventArgs e)
        {
            string clipboardText = System.Windows.Clipboard.GetText().Trim();
            if (string.IsNullOrEmpty(clipboardText) || clipboardText == lastClipboardUrl || !Uri.IsWellFormedUriString(clipboardText, UriKind.Absolute) || !clipboardText.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                return; // Skip if empty, unchanged, invalid, or not HTTP/HTTPS
            }

            if (await IsDownloadableUrl(clipboardText))
            {
                urlTextBox.Text = clipboardText;
                lastClipboardUrl = clipboardText; // Update last URL to prevent repetition
            }
        }

        private async Task<bool> IsDownloadableUrl(string url)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Head, url);
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"); // Avoid 403 errors from some servers

                HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                // Check if the response indicates a downloadable file
                if (response.Content.Headers.ContentDisposition != null && response.Content.Headers.ContentDisposition.DispositionType.Equals("attachment", StringComparison.OrdinalIgnoreCase))
                {
                    return true; // Content-Disposition: attachment indicates a download
                }

                string contentType = response.Content.Headers.ContentType?.MediaType?.ToLower();
                if (contentType != null && (
                    contentType.StartsWith("application/") || // e.g., application/octet-stream, application/pdf
                    contentType.StartsWith("audio/") ||       // e.g., audio/mpeg
                    contentType.StartsWith("video/") ||       // e.g., video/mp4
                    contentType.StartsWith("image/") ||       // e.g., image/jpeg (if you consider images downloadable)
                    contentType == "text/plain"))             // e.g., .txt files
                {
                    return true; // Likely a downloadable file
                }

                // Optionally check file extension if no clear headers
                string path = new Uri(url).AbsolutePath.ToLower();
                string[] downloadableExtensions = { ".zip", ".rar", ".exe", ".pdf", ".mp3", ".mp4", ".jpg", ".png", ".txt", ".docx" };
                if (downloadableExtensions.Any(ext => path.EndsWith(ext)))
                {
                    return true;
                }

                return false; // Default to false if no clear indication
            }
            catch (HttpRequestException)
            {
                return false; // Network error or invalid response
            }
            catch (TaskCanceledException)
            {
                return false; // Timeout
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking URL {url}: {ex.Message}");
                return false; // Other unexpected errors
            }
        }

        public void AddDownloadFromExternalWindow(string url)
        {
            string fileName = HandleFileExists(url);
            if (fileName != null)
            {
                Download download = new Download(url, fileName, DownloadPath);
                downloads.Add(download);
                downloadManager.AddDownload(download);
                download.PropertyChanged += Download_PropertyChanged;
                download.StartCommand.Execute(null);
                SaveDownloads();
            }
        }

        private void AddDownload_Click(object sender, RoutedEventArgs e)
        {
            string url = urlTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(url))
            {
                string fileName = HandleFileExists(url);
                if (fileName != null)
                {
                    Download download = new Download(url, fileName, DownloadPath);
                    downloads.Add(download);
                    downloadManager.AddDownload(download);
                    download.PropertyChanged += Download_PropertyChanged;
                    download.StartCommand.Execute(null);
                    urlTextBox.Clear();
                    SaveDownloads();
                }
            }
        }

        private string HandleFileExists(string url)
        {
            string fileName = Path.GetFileName(new Uri(url).LocalPath);
            string fullPath = Path.Combine(DownloadPath, fileName);
            int counter = 1;

            while (File.Exists(fullPath))
            {
                var existingDownload = downloads.FirstOrDefault(d => d.FullPath == fullPath);
                var result = System.Windows.MessageBox.Show("File already exists. Overwrite, rename, or skip?", "File Exists", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        if (existingDownload != null)
                        {
                            if (existingDownload.Status == "Downloading")
                            {
                                existingDownload.PauseCommand.Execute(null);
                                Thread.Sleep(500);
                            }
                            downloads.Remove(existingDownload);
                            downloadManager.RemoveDownload(existingDownload);
                        }
                        File.Delete(fullPath);
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show($"Error deleting: {ex.Message}");
                    }
                    break;
                }
                else if (result == MessageBoxResult.No)
                {
                    int lastDotIndex = fileName.LastIndexOf('.');
                    string baseName = lastDotIndex == -1 ? fileName : fileName.Substring(0, lastDotIndex);
                    string ext = lastDotIndex == -1 ? "" : fileName.Substring(lastDotIndex);
                    fileName = $"{baseName} ({counter++}){ext}";
                    fullPath = Path.Combine(DownloadPath, fileName);
                }
                else
                {
                    return null;
                }
            }
            return fileName;
        }

        private void StartAllDownloads_Click(object sender, RoutedEventArgs e) => downloadManager.StartAllDownloads();
        private void PauseAllDownloads_Click(object sender, RoutedEventArgs e) => downloadManager.PauseAllDownloads();
        private void ResumeAllDownloads_Click(object sender, RoutedEventArgs e) => downloadManager.ResumeAllDownloads();

        private void StartSelectedDownloads_Click(object sender, RoutedEventArgs e)
        {
            foreach (Download download in downloadListView.SelectedItems.OfType<Download>())
            {
                if (download.Status == "Queued" || download.IsPaused || download.HasError)
                    download.StartCommand.Execute(null);
            }
        }

        private void PauseSelectedDownloads_Click(object sender, RoutedEventArgs e)
        {
            foreach (Download download in downloadListView.SelectedItems.OfType<Download>())
            {
                if (download.Status == "Downloading")
                    download.PauseCommand.Execute(null);
            }
        }

        private void ResumeSelectedDownloads_Click(object sender, RoutedEventArgs e)
        {
            foreach (Download download in downloadListView.SelectedItems.OfType<Download>())
            {
                if (download.IsPaused || download.HasError)
                    download.ResumeCommand.Execute(null);
            }
        }

        private void ClearList_Click(object sender, RoutedEventArgs e)
        {
            downloads.Clear();
            downloadManager.ClearDownloads();
            SaveDownloads();
        }

        private void DeleteSelectedDownloads_Click(object sender, RoutedEventArgs e)
        {
            var selectedDownloads = downloadListView.SelectedItems.OfType<Download>().ToList();
            foreach (var download in selectedDownloads)
            {
                string fullPath = download.FullPath;
                if (deleteFileOnRemove && File.Exists(fullPath))
                {
                    var result = System.Windows.MessageBox.Show($"Delete '{download.FileName}' from disk?", "Confirm", MessageBoxButton.YesNo);
                    if (result == MessageBoxResult.Yes)
                    {
                        try
                        {
                            if (download.Status == "Downloading")
                            {
                                download.PauseCommand.Execute(null);
                                Thread.Sleep(500);
                            }
                            if (IsFileLocked(fullPath))
                            {
                                var forceResult = System.Windows.MessageBox.Show("File is in use by another process. Force close?", "File Locked", MessageBoxButton.YesNo);
                                if (forceResult == MessageBoxResult.Yes)
                                {
                                    KillProcessesUsingFile(fullPath);
                                }
                                else
                                {
                                    continue;
                                }
                            }
                            File.Delete(fullPath);
                        }
                        catch (Exception ex)
                        {
                            System.Windows.MessageBox.Show($"Error deleting: {ex.Message}");
                        }
                    }
                }
                downloads.Remove(download);
                downloadManager.RemoveDownload(download);
            }
            SaveDownloads();
        }

        private bool IsFileLocked(string filePath)
        {
            try
            {
                using (FileStream fs = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    return false;
                }
            }
            catch (IOException)
            {
                return true;
            }
        }

        private void KillProcessesUsingFile(string filePath)
        {
            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    if (process.MainModule != null && process.MainModule.FileName == filePath)
                    {
                        process.Kill();
                        process.WaitForExit(1000);
                    }
                }
                catch
                {
                }
            }
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow(downloadSubPath, deleteFileOnRemove, showDownloadNotifications);
            if (settingsWindow.ShowDialog() == true)
            {
                downloadSubPath = settingsWindow.DownloadSubPath; // Update subpath
                string fullDownloadPath = DownloadPath;
                if (!Directory.Exists(fullDownloadPath))
                {
                    Directory.CreateDirectory(fullDownloadPath);
                }
                deleteFileOnRemove = settingsWindow.DeleteFileOnRemove;
                showDownloadNotifications = settingsWindow.ShowDownloadNotifications;
                SaveSettings();

                // Update existing downloads with new path
                foreach (var download in downloads)
                {
                    download.DownloadPath = DownloadPath;
                }
                SaveDownloads();
            }
        }

        private void DownloadListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var originalSource = e.OriginalSource as DependencyObject;
            var listViewItem = originalSource != null ? FindAncestor<System.Windows.Controls.ListViewItem>(originalSource) : null;

            if (listViewItem != null && listViewItem.IsSelected)
            {
                var download = listViewItem.Content as Download;
                if (download != null && download.Status == "Completed")
                {
                    string fullPath = download.FullPath;
                    if (File.Exists(fullPath))
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = fullPath,
                                UseShellExecute = true
                            });
                        }
                        catch (Exception ex)
                        {
                            System.Windows.MessageBox.Show($"Error opening file: {ex.Message}");
                        }
                    }
                }
            }
        }

        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T ancestor)
                {
                    return ancestor;
                }
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private string FormatSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            while (number >= 1024 && counter < suffixes.Length - 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:F2} {suffixes[counter]}";
        }

        private void LoadSettings()
        {
            if (File.Exists(SETTINGS_FILE))
            {
                string json = File.ReadAllText(SETTINGS_FILE);
                var settings = JsonSerializer.Deserialize<Settings>(json);
                if (settings != null)
                {
                    // Use the saved subpath, or default if it’s an old full path
                    downloadSubPath = settings.DownloadSubPath.Contains("Users")
                        ? "Downloads\\MDM" // Reset if it’s an old full path
                        : settings.DownloadSubPath;
                    deleteFileOnRemove = settings.DeleteFileOnRemove;
                    showDownloadNotifications = settings.ShowDownloadNotifications;
                }
            }
            else
            {
                downloadSubPath = "Downloads\\MDM"; // Default subpath
                showDownloadNotifications = true;
            }

            // Ensure directory exists for this user
            string fullDownloadPath = DownloadPath;
            if (!Directory.Exists(fullDownloadPath))
            {
                Directory.CreateDirectory(fullDownloadPath);
            }
        }

        private void SaveSettings()
        {
            var settings = new Settings
            {
                DownloadSubPath = downloadSubPath, // Save only the subpath
                DeleteFileOnRemove = deleteFileOnRemove,
                ShowDownloadNotifications = showDownloadNotifications
            };
            File.WriteAllText(SETTINGS_FILE, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }

        private void LoadDownloads()
        {
            if (File.Exists(DOWNLOADS_FILE))
            {
                string json = File.ReadAllText(DOWNLOADS_FILE);
                var loadedDownloads = JsonSerializer.Deserialize<List<Download>>(json);
                if (loadedDownloads != null)
                {
                    foreach (var download in loadedDownloads)
                    {
                        download.DownloadPath = DownloadPath; // Update to current user’s path
                        download.InitFullPath(); // Rebuild FullPath
                        downloads.Add(download);
                        downloadManager.AddDownload(download);
                        download.PropertyChanged += Download_PropertyChanged;
                    }
                }
            }
        }

        private void SaveDownloads()
        {
            string json = JsonSerializer.Serialize(downloads.ToList(), new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(DOWNLOADS_FILE, json);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (downloads.Any(d => d.Status == "Downloading"))
            {
                var result = System.Windows.MessageBox.Show("Downloads are in progress. Do you want to pause and exit?", "Confirm Exit", MessageBoxButton.YesNoCancel);
                if (result == MessageBoxResult.Yes)
                {
                    downloadManager.PauseAllDownloads();
                    Thread.Sleep(500);
                }
                else if (result == MessageBoxResult.No || result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
            }
            SaveSettings();
            SaveDownloads();
            trayIcon.Visible = false;
            trayIcon.Dispose();
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            clipboardTimer.Stop();
            httpClient.Dispose(); // Clean up HttpClient
            base.OnClosed(e);
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
            }
            base.OnStateChanged(e);
        }
    }

    [Serializable]
    public class Settings
    {
        public string DownloadSubPath { get; set; } // Changed from DownloadPath
        public bool DeleteFileOnRemove { get; set; }
        public bool ShowDownloadNotifications { get; set; }
    }

    
}