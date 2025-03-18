using System;
using System.Collections.Generic;
using System.Linq;

namespace MDM
{
    public class DownloadManager
    {
        private List<Download> downloads;

        public DownloadManager()
        {
            downloads = new List<Download>();
        }

        public void AddDownload(Download download)
        {
            downloads.Add(download);
        }

        public void RemoveDownload(Download download)
        {
            downloads.Remove(download);
        }

        public void ClearDownloads()
        {
            downloads.Clear();
        }

        public void StartAllDownloads()
        {
            foreach (var download in downloads.Where(d => d.Status == "Queued"))
            {
                download.StartCommand.Execute(null);
            }
        }

        public void PauseAllDownloads()
        {
            foreach (var download in downloads.Where(d => d.Status == "Downloading"))
            {
                download.PauseCommand.Execute(null);
            }
        }

        public void ResumeAllDownloads()
        {
            foreach (var download in downloads.Where(d => d.Status == "Paused" || d.Status.StartsWith("Error")))
            {
                download.ResumeCommand.Execute(null);
            }
        }
    }
}