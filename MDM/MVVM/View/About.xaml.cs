using System;
using System.Windows;


namespace MDM.MVVM.View
{
    public partial class About : Window
    {
        public About()
        {
            InitializeComponent();

            tbVersionAuthor.Text = "Version 1.0\n\nCopyright \u00A9 " + DateTime.Now.Year + "\nMRK";
        }
    }
}
