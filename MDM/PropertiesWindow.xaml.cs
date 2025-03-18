using System.Windows;

namespace MDM
{
    public partial class PropertiesWindow : Window
    {
        public PropertiesWindow(Download download)
        {
            InitializeComponent();
            fileNameTextBlock.Text = download.FileName;
            urlTextBlock.Text = download.Url;
            statusTextBlock.Text = download.Status;
            sizeTextBlock.Text = FormatSize(download.TotalBytesToReceive);
            downloadedTextBlock.Text = FormatSize(download.BytesReceived);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
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
            return $"{number:0.##} {suffixes[counter]}";
        }
    }
}