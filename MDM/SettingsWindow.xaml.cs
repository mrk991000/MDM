using System.Windows;
using System.Windows.Forms;
using System.IO;

namespace MDM
{
    public partial class SettingsWindow : Window
    {
        public string DownloadSubPath { get; private set; } // Changed from DownloadPath
        public bool DeleteFileOnRemove { get; private set; }
        public bool ShowDownloadNotifications { get; private set; }

        public SettingsWindow(string currentDownloadSubPath, bool currentDeleteFileOnRemove, bool currentShowDownloadNotifications)
        {
            InitializeComponent();
            downloadPathTextBox.Text = currentDownloadSubPath; // Display subpath
            DownloadSubPath = currentDownloadSubPath;
            deleteFileOnRemoveCheckBox.IsChecked = currentDeleteFileOnRemove;
            DeleteFileOnRemove = currentDeleteFileOnRemove;
            showDownloadNotificationsCheckBox.IsChecked = currentShowDownloadNotifications;
            ShowDownloadNotifications = currentShowDownloadNotifications;
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.SelectedPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DownloadSubPath);
                DialogResult result = dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    // Convert absolute path to relative subpath
                    string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    string selectedPath = dialog.SelectedPath;
                    if (selectedPath.StartsWith(userProfile))
                    {
                        DownloadSubPath = selectedPath.Substring(userProfile.Length + 1); // Remove user profile part + "\"
                        downloadPathTextBox.Text = DownloadSubPath;
                    }
                    else
                    {
                        System.Windows.MessageBox.Show("Please select a folder within your user profile.");
                    }
                }
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            DownloadSubPath = downloadPathTextBox.Text;
            string fullPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DownloadSubPath);
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
            }
            DeleteFileOnRemove = deleteFileOnRemoveCheckBox.IsChecked.GetValueOrDefault();
            ShowDownloadNotifications = showDownloadNotificationsCheckBox.IsChecked.GetValueOrDefault();
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}