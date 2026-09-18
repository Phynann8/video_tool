using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Start.UI.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        public event EventHandler? BackRequested;

        [ObservableProperty]
        private string _defaultDownloadPath;

        public SettingsViewModel()
        {
            _defaultDownloadPath = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        }

        [RelayCommand]
        private void Back()
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void BrowseFolder()
        {
             var dialog = new Microsoft.Win32.OpenFolderDialog();
             dialog.Title = "Select Download Folder";
             dialog.Multiselect = false;
             
             if (dialog.ShowDialog() == true)
             {
                 DefaultDownloadPath = dialog.FolderName;
             }
        }
        
        [RelayCommand]
        private void SaveSettings()
        {
            // Persist settings to DB or Config file
        }
    }
}
