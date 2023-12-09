using MDM.MVVM.View;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Xml.Linq;
using MDM.Properties;
using Hardcodet.Wpf.TaskbarNotification;

using Application = System.Windows.Application;
using Panel = System.Windows.Controls.Panel;
using MenuItem = System.Windows.Controls.MenuItem;
using ContextMenu = System.Windows.Controls.ContextMenu;
using System.Windows.Interop;
using System.Drawing;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;


namespace MDM
{

    public partial class MainWindow : Window
    {
       // private List<string> propertyNames;
      //  private List<string> propertyValues;
        private string[] args;
        private bool miniTray = false;

        public MainWindow()
        {
            InitializeComponent();
            //System.Windows.MessageBox.Show(this, string.Format("Lanched Via Startup Arg:{0}", App.LaunchedViaStartup));

            args = Environment.GetCommandLineArgs();
            if (Settings.Default.ShowWindowOnStartup == false)
            {
                
                    this.Hide();
                    Application.Current.MainWindow.WindowState = WindowState.Minimized;
                    miniTray = true;
                
            
            }

            XNotifyIcon.Icon = new System.Drawing.Icon(@"../../../Resources/icon.ico");
            ContextMenu contextMenu = new ContextMenu();

            MenuItem exitMenuItem = new MenuItem();
            exitMenuItem.Header = "Exit";
            exitMenuItem.Click += new RoutedEventHandler(tcmExit_Click);

            MenuItem addNewMI = new MenuItem();
            addNewMI.Header = "Add New Download";
            addNewMI.Click += new RoutedEventHandler(addNewMI_Click);

            MenuItem resumeAllMI = new MenuItem();
            resumeAllMI.Header = "Resume All Downloads";
            resumeAllMI.Click += new RoutedEventHandler(cmStartAll_Click);

            MenuItem pauseAllMI = new MenuItem();
            pauseAllMI.Header = "Stop All Downloads";
            pauseAllMI.Click += new RoutedEventHandler(cmPauseAll_Click);

            contextMenu.Items.Add(addNewMI);
            contextMenu.Items.Add(resumeAllMI);
            contextMenu.Items.Add(pauseAllMI);
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(exitMenuItem);

            XNotifyIcon.ContextMenu = contextMenu;


            

            // Bind DownloadsList to downloadsGrid
            downloadsGrid.ItemsSource = DownloadManager.Instance.DownloadsList;
            DownloadManager.Instance.DownloadsList.CollectionChanged += new NotifyCollectionChangedEventHandler(DownloadsList_CollectionChanged);

            // In case of computer shutdown or restart, save current list of downloads to an XML file
            SystemEvents.SessionEnding += new SessionEndingEventHandler(SystemEvents_SessionEnding);



            // Load downloads from the XML file
            LoadDownloadsFromXml();

            if (DownloadManager.Instance.TotalDownloads == 0)
            {
                EnableMenuItems(false);

                // Clean temporary files in the download directory if no downloads were loaded
                if (Directory.Exists(Settings.Default.DownloadLocation))
                {
                    DirectoryInfo downloadLocation = new DirectoryInfo(Settings.Default.DownloadLocation);
                    foreach (FileInfo file in downloadLocation.GetFiles())
                    {
                        if (file.FullName.EndsWith(".tmp"))
                            file.Delete();
                    }
                }
            }

        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (miniTray == true)
            {
                if (WindowState == WindowState.Minimized) this.Hide();

                base.OnStateChanged(e);
            }
        }

        // Minimize to system tray when application is closed.
        protected override void OnClosing(CancelEventArgs e)
        {
            // setting cancel to true will cancel the close request
            // so the application is not closed
            e.Cancel = true;

            this.Hide();

            base.OnClosing(e);
        }



        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            miniTray = false;
            Application.Current.MainWindow.WindowState = WindowState.Minimized;
        }

        private void MinimizeToTray_Click(object sender, RoutedEventArgs e)
        {
            miniTray = true;
            Application.Current.MainWindow.WindowState = WindowState.Minimized;

        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            exitMenuItem_Click(sender,e);
        }
        private void settings_Click(object sender, RoutedEventArgs e)
        {
            Preferences preferences = new Preferences(false);
            preferences.ShowDialog();
        }
        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            this.Show();
            Application.Current.MainWindow.WindowState = WindowState.Normal;
        }
        private void exitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            string message = "Are you sure you want to exit the application?";

            MessageBoxResult result = System.Windows.MessageBox.Show(message, "", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.No)
            {
               
            }
            if (result == MessageBoxResult.Yes)
            {
                Application.Current.Shutdown();
            }

            SaveDownloadsToXml();
        }
        private void addNewMI_Click(object sender, RoutedEventArgs e)
        {
            NewDownload newDownloadDialog = new NewDownload(this);
            newDownloadDialog.ShowDialog();
        }

        private void about_Click(object sender, RoutedEventArgs e)
        {
            About about = new About();
            about.ShowDialog();
        }
        #region Methods



        #region Methods

        /// <summary>
        /// 
        /// </summary>
        private void SetEmptyPropertiesGrid()
        {
          /*  if (propertiesList.Count > 0)
                propertiesList.Clear();
            for (int i = 0; i < 6; i++)
            {
                propertiesList.Add(new PropertyModel(propertyNames[i], String.Empty));
            }

            propertiesGrid.Items.Refresh(); */
        }

        private void PauseAllDownloads()
        {
            if (downloadsGrid.Items.Count > 0)
            {
                foreach (WebDownloadClient download in DownloadManager.Instance.DownloadsList)
                {
                    download.Pause();
                }
            }
        }

        private void SaveDownloadsToXml()
        {
            if (DownloadManager.Instance.TotalDownloads > 0)
            {
                // Pause
                PauseAllDownloads();

                XElement root = new XElement("downloads");

                foreach (WebDownloadClient download in DownloadManager.Instance.DownloadsList)
                {
                    string username = String.Empty;
                    string password = String.Empty;
                    if (download.ServerLogin != null)
                    {
                        username = download.ServerLogin.UserName;
                        password = download.ServerLogin.Password;
                    }

                    XElement xdl = new XElement("download",
                                        new XElement("file_name", download.FileName),
                                        new XElement("url", download.Url.ToString()),
                                        new XElement("username", username),
                                        new XElement("password", password),
                                        new XElement("temp_path", download.TempDownloadPath),
                                        new XElement("file_size", download.FileSize),
                                        new XElement("downloaded_size", download.DownloadedSize),
                                        new XElement("status", download.Status.ToString()),
                                        new XElement("status_text", download.StatusText),
                                        new XElement("total_time", download.TotalElapsedTime.ToString()),
                                        new XElement("added_on", download.AddedOn.ToString()),
                                        new XElement("completed_on", download.CompletedOn.ToString()),
                                        new XElement("supports_resume", download.SupportsRange.ToString()),
                                        new XElement("has_error", download.HasError.ToString()),
                                        new XElement("open_file", download.OpenFileOnCompletion.ToString()),
                                        new XElement("temp_created", download.TempFileCreated.ToString()),
                                        new XElement("is_batch", download.IsBatch.ToString()),
                                        new XElement("url_checked", download.BatchUrlChecked.ToString()));
                    root.Add(xdl);
                }

                XDocument xd = new XDocument();
                xd.Add(root);
                // Save downloads to XML file
                xd.Save("Downloads.xml");
            }
        }

        private void LoadDownloadsFromXml()
        {
            try
            {
                if (File.Exists("Downloads.xml"))
                {
                    // Load downloads from XML file
                    XElement downloads = XElement.Load("Downloads.xml");
                    if (downloads.HasElements)
                    {
                        IEnumerable<XElement> downloadsList =
                            from el in downloads.Elements()
                            select el;
                        foreach (XElement download in downloadsList)
                        {
                            // Create WebDownloadClient object based on XML data
                            WebDownloadClient downloadClient = new WebDownloadClient(download.Element("url").Value);

                            downloadClient.FileName = download.Element("file_name").Value;

                            downloadClient.DownloadProgressChanged += downloadClient.DownloadProgressChangedHandler;
                            downloadClient.DownloadCompleted += downloadClient.DownloadCompletedHandler;
                            downloadClient.PropertyChanged += this.PropertyChangedHandler;
                            downloadClient.StatusChanged += this.StatusChangedHandler;
                            downloadClient.DownloadCompleted += this.DownloadCompletedHandler;

                            string username = download.Element("username").Value;
                            string password = download.Element("password").Value;
                            if (username != String.Empty && password != String.Empty)
                            {
                                downloadClient.ServerLogin = new NetworkCredential(username, password);
                            }

                            downloadClient.TempDownloadPath = download.Element("temp_path").Value;
                            downloadClient.FileSize = Convert.ToInt64(download.Element("file_size").Value);
                            downloadClient.DownloadedSize = Convert.ToInt64(download.Element("downloaded_size").Value);

                            DownloadManager.Instance.DownloadsList.Add(downloadClient);

                            if (download.Element("status").Value == "Completed")
                            {
                                downloadClient.Status = DownloadStatus.Completed;
                            }
                            else
                            {
                                downloadClient.Status = DownloadStatus.Paused;
                            }

                            downloadClient.StatusText = download.Element("status_text").Value;

                            downloadClient.ElapsedTime = TimeSpan.Parse(download.Element("total_time").Value);
                            downloadClient.AddedOn = DateTime.Parse(download.Element("added_on").Value);
                            downloadClient.CompletedOn = DateTime.Parse(download.Element("completed_on").Value);

                            downloadClient.SupportsRange = Boolean.Parse(download.Element("supports_resume").Value);
                            downloadClient.HasError = Boolean.Parse(download.Element("has_error").Value);
                            downloadClient.OpenFileOnCompletion = Boolean.Parse(download.Element("open_file").Value);
                            downloadClient.TempFileCreated = Boolean.Parse(download.Element("temp_created").Value);
                            downloadClient.IsBatch = Boolean.Parse(download.Element("is_batch").Value);
                            downloadClient.BatchUrlChecked = Boolean.Parse(download.Element("url_checked").Value);

                            if (downloadClient.Status == DownloadStatus.Paused && !downloadClient.HasError && Settings.Default.StartDownloadsOnStartup)
                            {
                                downloadClient.Start();
                            }
                        }

                        // Create empty XML file
                        XElement root = new XElement("downloads");
                        XDocument xd = new XDocument();
                        xd.Add(root);
                        xd.Save("Downloads.xml");
                    }
                }
            }
            catch (Exception)
            {
                System.Windows.MessageBox.Show("There was an error while loading the download list.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EnableMenuItems(bool enabled)
        {
            btnDelete.IsEnabled = enabled;
            btnClearCompleted.IsEnabled = enabled;
            btnStart.IsEnabled = enabled;
            btnPause.IsEnabled = enabled;
            btnStartAll.IsEnabled = enabled;
            btnPauseAll.IsEnabled = enabled;
            btnRestart.IsEnabled = enabled;
        }

        private void EnableDataGridMenuItems(bool enabled)
        {
            cmStart.IsEnabled = enabled;
            cmPause.IsEnabled = enabled;
            cmDelete.IsEnabled = enabled;
            cmRestart.IsEnabled = enabled;
            cmOpenFile.IsEnabled = enabled;
            cmOpenDownloadFolder.IsEnabled = enabled;
            cmStartAll.IsEnabled = enabled;
            cmPauseAll.IsEnabled = enabled;
            cmSelectAll.IsEnabled = enabled;
            cmCopyURLtoClipboard.IsEnabled = enabled;
            cmProperties.IsEnabled = enabled;
        }

        #endregion

        #region Main Window Event Handlers

        private void mainWindow_ContentRendered(object sender, EventArgs e)
        {
            // In case the application was started from a web browser and receives command-line arguments
            if (args.Length == 2)
            {
                if (args[1].StartsWith("http"))
                {
                    System.Windows.Clipboard.SetText(args[1]);

                    NewDownload newDownloadDialog = new NewDownload(this);
                    newDownloadDialog.ShowDialog();
                }
            }
        }

        private void mainWindow_StateChanged(object sender, EventArgs e)
        {
            
        }
        private void mainWindow_Closing(object sender, CancelEventArgs e)
        {
            
        
              

            SaveDownloadsToXml();
        }

        private void SystemEvents_SessionEnding(object sender, SessionEndingEventArgs e)
        {
            SaveDownloadsToXml();
        }

        private void mainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Ctrl + A selects all downloads in the list
            if ((Keyboard.Modifiers == ModifierKeys.Control) && (e.Key == Key.A))
            {
                this.downloadsGrid.SelectAll();
            }
        }

        private void downloadsGrid_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Delete key clears selected downloads
            if (e.Key == Key.Delete)
            {
                btnDelete_Click(sender, e);
            }
        }

        private void downloadsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (downloadsGrid.SelectedItems.Count > 0)
            {
                foreach (WebDownloadClient downld in DownloadManager.Instance.DownloadsList)
                {
                    downld.IsSelected = false;
                }

                var download = (WebDownloadClient)downloadsGrid.SelectedItem;

           /*     if (propertyValues.Count > 0)
                    propertyValues.Clear();

                propertyValues.Add(download.Url.ToString());
                string resumeSupported = "No";
                if (download.SupportsRange)
                    resumeSupported = "Yes";
                propertyValues.Add(resumeSupported);
                propertyValues.Add(download.FileType);
                propertyValues.Add(download.DownloadFolder);
                propertyValues.Add(download.AverageDownloadSpeed);
                propertyValues.Add(download.TotalElapsedTimeString);
           */
              /*  if (propertiesList.Count > 0)
                    propertiesList.Clear();

                for (int i = 0; i < 6; i++)
                {
                    propertiesList.Add(new PropertyModel(propertyNames[i], propertyValues[i]));
                }
              */
                //propertiesGrid.Items.Refresh();
                download.IsSelected = true;
            }
            else
            {
                if (DownloadManager.Instance.TotalDownloads > 0)
                {
                    foreach (WebDownloadClient downld in DownloadManager.Instance.DownloadsList)
                    {
                        downld.IsSelected = false;
                    }
                }
                SetEmptyPropertiesGrid();
            }
        }

        public void PropertyChangedHandler(object sender, PropertyChangedEventArgs e)
        {
            WebDownloadClient download = (WebDownloadClient)sender;
            if (e.PropertyName == "AverageSpeedAndTotalTime" && download.Status != DownloadStatus.Deleting)
            {
                this.Dispatcher.Invoke(new PropertyChangedEventHandler(UpdatePropertiesList), sender, e);
            }
        }

        private void UpdatePropertiesList(object sender, PropertyChangedEventArgs e)
        {
            //propertyValues.RemoveRange(4, 2);
            var download = (WebDownloadClient)downloadsGrid.SelectedItem;
            speedText.Text = download.AverageDownloadSpeed;
            allPercentText.Text = download.PercentString;
            allProgress.Visibility = Visibility.Visible;
            allProgress.Value = download.Progress;
            //propertyValues.Add(download.AverageDownloadSpeed);
            //propertyValues.Add(download.TotalElapsedTimeString);

            //propertiesList.RemoveRange(4, 2);
          //  propertiesList.Add(new PropertyModel(propertyNames[4], propertyValues[4]));
         //   propertiesList.Add(new PropertyModel(propertyNames[5], propertyValues[5]));
            //propertiesGrid.Items.Refresh();
        }

        private void downloadsGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            dgScrollViewer.ScrollToVerticalOffset(dgScrollViewer.VerticalOffset - e.Delta / 3);
        }

        private void propertiesGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
          //  propertiesScrollViewer.ScrollToVerticalOffset(propertiesScrollViewer.VerticalOffset - e.Delta / 3);
        }

        private void downloadsGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (DownloadManager.Instance.TotalDownloads == 0)
            {
                EnableDataGridMenuItems(false);
            }
            else
            {
                if (downloadsGrid.SelectedItems.Count == 1)
                {
                    EnableDataGridMenuItems(true);
                }
                else if (downloadsGrid.SelectedItems.Count > 1)
                {
                    EnableDataGridMenuItems(true);
                    cmOpenFile.IsEnabled = false;
                    cmOpenDownloadFolder.IsEnabled = false;
                    cmCopyURLtoClipboard.IsEnabled = false;
                    cmProperties.IsEnabled = false;
                }
                else
                {
                    EnableDataGridMenuItems(false);
                    cmStartAll.IsEnabled = true;
                    cmPauseAll.IsEnabled = true;
                    cmSelectAll.IsEnabled = true;
                }
            }
        }

        private void DownloadsList_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (DownloadManager.Instance.TotalDownloads == 1)
            {
                EnableMenuItems(true);
                downloadsText.Text = "1 Download";
            }
            else if (DownloadManager.Instance.TotalDownloads > 1)
            {
                EnableMenuItems(true);
                downloadsText.Text = DownloadManager.Instance.TotalDownloads + " Downloads";
            }
            else
            {
                EnableMenuItems(false);
                downloadsText.Text = "Ready";
            }
        }

        public void StatusChangedHandler(object sender, EventArgs e)
        {
            this.Dispatcher.Invoke(new EventHandler(StatusChanged), sender, e);
        }

        private void StatusChanged(object sender, EventArgs e)
        {
            // Start the first download in the queue, if it exists
            WebDownloadClient dl = (WebDownloadClient)sender;
            if (dl.Status == DownloadStatus.Paused || dl.Status == DownloadStatus.Completed
                || dl.Status == DownloadStatus.Deleted || dl.HasError)
            {
                foreach (WebDownloadClient d in DownloadManager.Instance.DownloadsList)
                {
                    if (d.Status == DownloadStatus.Queued)
                    {
                        d.Start();
                        break;
                    }
                }
            }

            foreach (WebDownloadClient d in DownloadManager.Instance.DownloadsList)
            {
                if (d.Status == DownloadStatus.Downloading)
                {
                    d.SpeedLimitChanged = true;
                }
            }

            int active = DownloadManager.Instance.ActiveDownloads;
            int completed = DownloadManager.Instance.CompletedDownloads;

            if (active > 0)
            {
                if (completed == 0)
                    downloadsText.Text = " (" + active + " Active)";
                else
                    downloadsText.Text = " (" + active + " Active, ";
            }
            else
                downloadsText.Text = String.Empty;

            if (completed > 0)
            {
                if (active == 0)
                    downloadsText.Text = " (" + completed + " Completed)";
                else
                    downloadsText.Text = completed + " Completed)";
            }
            else
                downloadsText.Text = String.Empty;
        }

        public void DownloadCompletedHandler(object sender, EventArgs e)
        {
            
                WebDownloadClient download = (WebDownloadClient)sender;

                if (download.Status == DownloadStatus.Completed)
                {
                 string title = "Download Completed";
                 string text = download.FileName + " has finished downloading.";
                //System.Windows.MessageBox.Show("Download " + download.FileName + " Completed", "Download Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                

                    XNotifyIcon.ShowBalloonTip(title, text, BalloonIcon.Info);
                }
            
        }

        #endregion

        #region Click Event Handlers

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (downloadsGrid.SelectedItems.Count > 0)
            {
                MessageBoxResult result = MessageBoxResult.None;
                if (Settings.Default.ConfirmDelete)
                {
                    string message = "Are you sure you want to delete the selected download(s)?";
                    result = System.Windows.MessageBox.Show(message, "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                }

                if (result == MessageBoxResult.Yes || !Settings.Default.ConfirmDelete)
                {
                    var selectedDownloads = downloadsGrid.SelectedItems.Cast<WebDownloadClient>();
                    var downloadsToDelete = new List<WebDownloadClient>();

                    foreach (WebDownloadClient download in selectedDownloads)
                    {
                        if (download.HasError || download.Status == DownloadStatus.Paused || download.Status == DownloadStatus.Queued)
                        {
                            if (File.Exists(download.TempDownloadPath))
                            {
                                File.Delete(download.TempDownloadPath);
                            }
                            download.Status = DownloadStatus.Deleting;
                            downloadsToDelete.Add(download);
                        }
                        else if (download.Status == DownloadStatus.Completed)
                        {
                            download.Status = DownloadStatus.Deleting;
                            downloadsToDelete.Add(download);
                        }
                        else
                        {
                            download.Status = DownloadStatus.Deleting;
                            while (true)
                            {
                                if (download.DownloadThread.ThreadState == System.Threading.ThreadState.Stopped)
                                {
                                    if (File.Exists(download.TempDownloadPath))
                                    {
                                        File.Delete(download.TempDownloadPath);
                                    }
                                    downloadsToDelete.Add(download);
                                    break;
                                }
                            }
                        }
                    }

                    foreach (var download in downloadsToDelete)
                    {
                        download.Status = DownloadStatus.Deleted;
                        DownloadManager.Instance.DownloadsList.Remove(download);
                    }
                }
            }
        }

        private void btnClearCompleted_Click(object sender, RoutedEventArgs e)
        {
            if (DownloadManager.Instance.TotalDownloads > 0)
            {
                var downloadsToClear = new List<WebDownloadClient>();

                foreach (var download in DownloadManager.Instance.DownloadsList)
                {
                    if (download.Status == DownloadStatus.Completed)
                    {
                        download.Status = DownloadStatus.Deleting;
                        downloadsToClear.Add(download);
                    }
                }

                foreach (var download in downloadsToClear)
                {
                    download.Status = DownloadStatus.Deleted;
                    DownloadManager.Instance.DownloadsList.Remove(download);
                }
            }
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            if (downloadsGrid.SelectedItems.Count > 0)
            {
                var selectedDownloads = downloadsGrid.SelectedItems.Cast<WebDownloadClient>();

                foreach (WebDownloadClient download in selectedDownloads)
                {
                    if (download.Status == DownloadStatus.Paused || download.HasError)
                    {
                        download.Start();
                    }
                }
            }
        }

        private void btnPause_Click(object sender, RoutedEventArgs e)
        {
            if (downloadsGrid.SelectedItems.Count > 0)
            {
                var selectedDownloads = downloadsGrid.SelectedItems.Cast<WebDownloadClient>();

                foreach (WebDownloadClient download in selectedDownloads)
                {
                    download.Pause();
                }
            }
        }


        private void cmRestart_Click(object sender, RoutedEventArgs e)
        {
            if (downloadsGrid.SelectedItems.Count > 0)
            {
                var selectedDownloads = downloadsGrid.SelectedItems.Cast<WebDownloadClient>();

                foreach (WebDownloadClient download in selectedDownloads)
                {
                    download.Restart();
                }
            }
        }

        private void cmOpenFile_Click(object sender, RoutedEventArgs e)
        {
            if (downloadsGrid.SelectedItems.Count == 1)
            {
                var download = (WebDownloadClient)downloadsGrid.SelectedItem;
                if (download.Status == DownloadStatus.Completed && File.Exists(download.DownloadPath))
                {
                    Process.Start(@download.DownloadPath);
                }
            }
        }

        private void cmOpenDownloadFolder_Click(object sender, RoutedEventArgs e)
        {
            if (downloadsGrid.SelectedItems.Count == 1)
            {
                var download = (WebDownloadClient)downloadsGrid.SelectedItem;
                int lastIndex = download.DownloadPath.LastIndexOf("\\");
                string directory = download.DownloadPath.Remove(lastIndex + 1);
                if (Directory.Exists(directory))
                {
                    Process.Start(@directory);
                }
            }
        }

        private void cmStartAll_Click(object sender, RoutedEventArgs e)
        {
            if (downloadsGrid.Items.Count > 0)
            {
                foreach (WebDownloadClient download in DownloadManager.Instance.DownloadsList)
                {
                    if (download.Status == DownloadStatus.Paused || download.HasError)
                    {
                        download.Start();
                    }
                }
            }
        }

        private void cmPauseAll_Click(object sender, RoutedEventArgs e)
        {
            PauseAllDownloads();
        }

        

        private void cmSelectAll_Click(object sender, RoutedEventArgs e)
        {
            if (downloadsGrid.Items.Count > 0)
            {
                if (downloadsGrid.SelectedItems.Count < downloadsGrid.Items.Count)
                {
                    downloadsGrid.SelectAll();
                }
            }
        }

        private void cmCopyURLtoClipboard_Click(object sender, RoutedEventArgs e)
        {
            if (downloadsGrid.SelectedItems.Count == 1)
            {
                var download = (WebDownloadClient)downloadsGrid.SelectedItem;
                System.Windows.Clipboard.SetText(download.Url.ToString());
            }
        }



        private void cmProperties_Click(object sender, RoutedEventArgs e)
        {

            if (downloadsGrid.SelectedItems.Count == 1)
            {
                var download = (WebDownloadClient)downloadsGrid.SelectedItem;
                string name = download.FileName.ToString();
                string url = download.Url.ToString();
                string downloadLocation = Settings.Default.DownloadLocation.ToString() + download.FileName.ToString();
                string status = download.StatusText;
                string size = download.FileSizeString.ToString();
                string downloaded = download.DownloadedSizeString;
                string percent = download.PercentString;
                float progress = download.Progress;
                string speed = download.DownloadSpeed.ToString();
                string timeLeft = download.TimeLeft.ToString();
                string addedOn = download.AddedOnString;
                string completedOn = download.CompletedOnString;



                Props newFileProps = new Props(this, name,url,downloadLocation,status,size,downloaded,percent,progress,speed,timeLeft,addedOn,completedOn);
                newFileProps.ShowDialog();
            }
        }
        private void allClick(object sender, RoutedEventArgs e)
        {
            downloadsGrid.ItemsSource = DownloadManager.Instance.DownloadsList;
        }


        private void inProgressClick(object sender, RoutedEventArgs e)
        {
            var filteredList = DownloadManager.Instance.DownloadsList.Where(x => x.Status.ToString().Equals("Downloading"));

            downloadsGrid.ItemsSource = null;
            downloadsGrid.ItemsSource = filteredList;
        }

        private void stoppedClick(object sender, RoutedEventArgs e)
        {
            var filteredList = DownloadManager.Instance.DownloadsList.Where(x => x.Status.ToString().Equals("Paused"));

            downloadsGrid.ItemsSource = null;
            downloadsGrid.ItemsSource = filteredList;
        }
        private void downloadedClick(object sender, RoutedEventArgs e)
        {
            var filteredList = DownloadManager.Instance.DownloadsList.Where(x => x.Status.ToString().Equals("Completed"));

            downloadsGrid.ItemsSource = null;
            downloadsGrid.ItemsSource = filteredList;
        }
        private void failedClick(object sender, RoutedEventArgs e)
        {
            var filteredList = DownloadManager.Instance.DownloadsList.Where(x => x.Status.ToString().Equals("Error"));

            downloadsGrid.ItemsSource = null;
            downloadsGrid.ItemsSource = filteredList;
        }





        private void tcmExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void tcmShowMainWindow_Click(object sender, RoutedEventArgs e)
        {
            
        }

        #endregion

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var tbx = sender as System.Windows.Controls.TextBox;
            if(tbx.Text != "")
            {
                var filteredList = DownloadManager.Instance.DownloadsList.Where(x => x.FileName.Contains(tbx.Text));

                downloadsGrid.ItemsSource = null;
                downloadsGrid.ItemsSource = filteredList;
            }
            else
            {
                downloadsGrid.ItemsSource = DownloadManager.Instance.DownloadsList;
            }
        }
    }
 #endregion
}