using System;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Start.Core.Interfaces;
using Start.Infrastructure.Data;
using Start.Infrastructure.Services;
using Start.App.Services;
using Start.UI.ViewModels;
using Start.UI.Views;

namespace Start.UI
{
    public partial class App : Application
    {
        private IServiceProvider _serviceProvider;

        public App()
        {
            ServiceCollection services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();
        }

        private void ConfigureServices(ServiceCollection services)
        {
            // Database Path
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dbFolder = Path.Combine(appData, "UniversalMediaDownloader");
            Directory.CreateDirectory(dbFolder);
            string dbPath = Path.Combine(dbFolder, "media_downloader.db");

            // Infrastructure & Core Services
            services.AddSingleton<IDownloadRepository>(provider => new SqliteDownloadRepository(dbPath));
            services.AddSingleton<ISettingsRepository>(provider => new SqliteSettingsRepository(dbPath));
            services.AddSingleton<DatabaseBootstrap>(provider => new DatabaseBootstrap(dbPath));
            
            services.AddSingleton<DramaboxClient>();
            services.AddSingleton<IExtractorEngine>(provider => new YtDlpExtractorEngine(provider.GetRequiredService<ISettingsRepository>()));
            services.AddSingleton<IExtractorEngine, DramaBoxExtractorEngine>();
            services.AddSingleton<IExtractorEngine, IflixExtractorEngine>();
            services.AddSingleton<IExtractorEngine, KissKhExtractorEngine>();
            services.AddSingleton<YtDlpDownloadEngine>();
            services.AddSingleton<HttpDownloadEngine>();
            services.AddSingleton<IDownloadEngine, RoutingDownloadEngine>();
            services.AddSingleton<IMediaProcessor, FfmpegMediaProcessor>();
            
            // App Services
            services.AddSingleton<IQueueManager, QueueManager>();
            services.AddSingleton<IDownloadService, DownloadService>();

            // Register ServiceProvider for ViewModels that need to resolve dependencies
            services.AddSingleton<IServiceProvider>(sp => sp);
            
            // UI ViewModels
            services.AddTransient<MainViewModel>();
            services.AddTransient<AddDownloadViewModel>();
            services.AddTransient<QueueViewModel>();
            services.AddTransient<HistoryViewModel>();
            services.AddTransient<SettingsViewModel>();

            // UI Views
            services.AddTransient<Views.MainWindow>();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            try
            {
                // Initialize Database
                var dbBootstrap = _serviceProvider.GetRequiredService<DatabaseBootstrap>();
                await dbBootstrap.SetupAsync();

                var mainWindow = _serviceProvider.GetRequiredService<Views.MainWindow>();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start application:\n{ex.Message}\n\n{ex.StackTrace}", 
                    "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }
    }
}
