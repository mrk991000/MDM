using MDM.MVVM.View;
using MDM.Properties;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;

namespace MDM.MVVM.View
{
    public partial class Props : Window
    {
        private MainWindow mainWindow;

        public Props(MainWindow mainWin,string name,string url,string downloadLocation,string status,string size,string downloaded,string percent,float progress,string speed,string timeLeft,string addedOn,string completedOn)
        {
            InitializeComponent();
            mainWindow = mainWin;
            downloc.Text = downloadLocation;
            urlText.Text = url;
            fileName.Text = name;
            statusText.Text = status;
            sizeText.Text = size;
            downloadedText.Text = downloaded;
            percentText.Text = percent;
            speedText.Text = speed;
            timeLeftText.Text = timeLeft;
            addedOnText.Text = addedOn;
            completedOnText.Text = completedOn;
            progress_bar.Value = progress;

        }
    }
}
