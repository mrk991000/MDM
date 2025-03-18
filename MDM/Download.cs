using System;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using GalaSoft.MvvmLight.Command;

namespace MDM
{
    [Serializable]
    public class Download : INotifyPropertyChanged
    {
        private string url;
        private string fileName;
        private string status;
        private double progress;
        private string downloadPath;
        private string fullPath;
        private long bytesReceived;
        private long totalBytesToReceive;
        private TimeSpan timeRemaining;
        private double downloadSpeed;
        private bool isPaused;
        private bool hasError;

        [NonSerialized]
        private CancellationTokenSource cts;
        [NonSerialized]
        private Task downloadTask;
        [NonSerialized]
        private DateTime downloadStartTime;
        [NonSerialized]
        private ICommand startCommand;
        [NonSerialized]
        private ICommand pauseCommand;
        [NonSerialized]
        private ICommand resumeCommand;

        public string Url
        {
            get => url;
            set { url = value; OnPropertyChanged(); }
        }

        public string FileName
        {
            get => fileName;
            set { fileName = value; OnPropertyChanged(); }
        }

        public string Status
        {
            get => status;
            set { status = value; OnPropertyChanged(); }
        }

        public double Progress
        {
            get => progress;
            set { progress = value; OnPropertyChanged(); }
        }

        public string DownloadPath
        {
            get => downloadPath;
            set { downloadPath = value; OnPropertyChanged(); InitFullPath(); }
        }

        public string FullPath
        {
            get => fullPath;
            set { fullPath = value; OnPropertyChanged(); }
        }

        public long BytesReceived
        {
            get => bytesReceived;
            set { bytesReceived = value; OnPropertyChanged(); }
        }

        public long TotalBytesToReceive
        {
            get => totalBytesToReceive;
            set { totalBytesToReceive = value; OnPropertyChanged(); }
        }

        public TimeSpan TimeRemaining
        {
            get => timeRemaining;
            set { timeRemaining = value; OnPropertyChanged(); }
        }

        public double DownloadSpeed
        {
            get => downloadSpeed;
            set { downloadSpeed = value; OnPropertyChanged(); }
        }

        public bool IsPaused
        {
            get => isPaused;
            set { isPaused = value; OnPropertyChanged(); }
        }

        public bool HasError
        {
            get => hasError;
            set { hasError = value; OnPropertyChanged(); }
        }

        public ICommand StartCommand => startCommand ??= new RelayCommand(StartDownload, CanStartDownload);
        public ICommand PauseCommand => pauseCommand ??= new RelayCommand(PauseDownload, CanPauseDownload);
        public ICommand ResumeCommand => resumeCommand ??= new RelayCommand(ResumeDownload, CanResumeDownload);

        public Download()
        {
            // For deserialization
        }

        public Download(string url, string fileName, string downloadPath)
        {
            Url = url;
            FileName = fileName;
            DownloadPath = downloadPath;
            InitFullPath();
            Status = "Queued";
            Progress = 0;
            BytesReceived = File.Exists(fullPath) ? new FileInfo(fullPath).Length : 0;
            TotalBytesToReceive = 0;
            DownloadSpeed = 0;
            TimeRemaining = TimeSpan.Zero;
            IsPaused = false;
            HasError = false;
        }

        public void InitFullPath() // Changed to public
        {
            if (!string.IsNullOrEmpty(DownloadPath) && !string.IsNullOrEmpty(FileName))
            {
                FullPath = Path.Combine(DownloadPath, FileName);
            }
        }

        private async void StartDownload()
        {
            Status = "Downloading";
            HasError = false;
            downloadStartTime = DateTime.Now;
            cts = new CancellationTokenSource();
            downloadTask = DownloadFileAsync(cts.Token);
            try
            {
                await downloadTask;
                if (!cts.IsCancellationRequested)
                {
                    Status = "Completed";
                    Progress = 100;
                    DownloadSpeed = 0;
                    TimeRemaining = TimeSpan.Zero;
                    IsPaused = false;
                }
            }
            catch (OperationCanceledException)
            {
                if (IsPaused)
                {
                    Status = "Paused";
                    DownloadSpeed = 0;
                }
            }
            catch (Exception ex)
            {
                Status = $"Error: {ex.Message}";
                HasError = true;
                DownloadSpeed = 0;
                TimeRemaining = TimeSpan.Zero;
            }
        }

        private void PauseDownload()
        {
            if (cts != null && !cts.IsCancellationRequested)
            {
                cts.Cancel();
                IsPaused = true;
                Status = "Paused";
                DownloadSpeed = 0;
            }
        }

        private async void ResumeDownload()
        {
            if (IsPaused || HasError)
            {
                Status = "Downloading";
                HasError = false;
                IsPaused = false;
                downloadStartTime = DateTime.Now;
                cts = new CancellationTokenSource();
                downloadTask = DownloadFileAsync(cts.Token);
                try
                {
                    await downloadTask;
                    if (!cts.IsCancellationRequested)
                    {
                        Status = "Completed";
                        Progress = 100;
                        DownloadSpeed = 0;
                        TimeRemaining = TimeSpan.Zero;
                    }
                }
                catch (OperationCanceledException)
                {
                    if (IsPaused)
                    {
                        Status = "Paused";
                        DownloadSpeed = 0;
                    }
                }
                catch (Exception ex)
                {
                    Status = $"Error: {ex.Message}";
                    HasError = true;
                    DownloadSpeed = 0;
                    TimeRemaining = TimeSpan.Zero;
                }
            }
        }

        private bool CanStartDownload() => Status == "Queued" || IsPaused || HasError;
        private bool CanPauseDownload() => Status == "Downloading";
        private bool CanResumeDownload() => IsPaused || HasError;

        private async Task DownloadFileAsync(CancellationToken token)
        {
            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) })
            {
                if (TotalBytesToReceive == 0)
                {
                    var headResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, Url), token);
                    TotalBytesToReceive = headResponse.Content.Headers.ContentLength ?? 0;
                }

                client.DefaultRequestHeaders.Range = new System.Net.Http.Headers.RangeHeaderValue(BytesReceived, null);
                using (var response = await client.GetAsync(Url, HttpCompletionOption.ResponseHeadersRead, token))
                {
                    response.EnsureSuccessStatusCode();
                    long serverSize = response.Content.Headers.ContentLength ?? TotalBytesToReceive;
                    if (TotalBytesToReceive == 0 || serverSize > TotalBytesToReceive) TotalBytesToReceive = serverSize;

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(FullPath, FileMode.Append, FileAccess.Write, FileShare.None))
                    {
                        byte[] buffer = new byte[8192];
                        int bytesRead;
                        DateTime lastUpdate = DateTime.Now;
                        long lastBytesReceived = BytesReceived;

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead, token);
                            BytesReceived += bytesRead;
                            Progress = TotalBytesToReceive > 0 ? (double)BytesReceived / TotalBytesToReceive * 100 : 0;

                            var now = DateTime.Now;
                            if ((now - lastUpdate).TotalSeconds >= 1)
                            {
                                double secondsElapsed = (now - lastUpdate).TotalSeconds;
                                double bytesPerSecond = (BytesReceived - lastBytesReceived) / secondsElapsed;
                                DownloadSpeed = bytesPerSecond;
                                double remainingBytes = TotalBytesToReceive - BytesReceived;
                                TimeRemaining = remainingBytes > 0 ? TimeSpan.FromSeconds(remainingBytes / bytesPerSecond) : TimeSpan.Zero;
                                lastUpdate = now;
                                lastBytesReceived = BytesReceived;
                            }

                            token.ThrowIfCancellationRequested();
                        }
                    }
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}