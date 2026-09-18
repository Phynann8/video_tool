using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Start.Core.Models;

namespace Start.UI.Converters
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return b ? Visibility.Visible : Visibility.Collapsed;
            if (value is string s)
                return string.IsNullOrEmpty(s) ? Visibility.Collapsed : Visibility.Visible;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility v && v == Visibility.Visible;
        }
    }

    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return b ? Visibility.Collapsed : Visibility.Visible;
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility v && v != Visibility.Visible;
        }
    }
    
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return !b;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return !b;
            return false;
        }
    }

    public class StatusToColorBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush DownloadingBrush = new(Color.FromRgb(0x00, 0xce, 0xc9)); // Cyan
        private static readonly SolidColorBrush QueuedBrush = new(Color.FromRgb(0x74, 0xb9, 0xff)); // Light Blue
        private static readonly SolidColorBrush PausedBrush = new(Color.FromRgb(0xfd, 0xcb, 0x6e)); // Amber
        private static readonly SolidColorBrush FailedBrush = new(Color.FromRgb(0xff, 0x76, 0x75)); // Coral Red
        private static readonly SolidColorBrush CompletedBrush = new(Color.FromRgb(0x00, 0xb8, 0x94)); // Mint Green
        private static readonly SolidColorBrush DefaultBrush = new(Color.FromRgb(0xdc, 0xdc, 0xdc));

        static StatusToColorBrushConverter()
        {
            DownloadingBrush.Freeze();
            QueuedBrush.Freeze();
            PausedBrush.Freeze();
            FailedBrush.Freeze();
            CompletedBrush.Freeze();
            DefaultBrush.Freeze();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is JobStatus status)
            {
                return status switch
                {
                    JobStatus.Downloading => DownloadingBrush,
                    JobStatus.Queued or JobStatus.PendingAnalysis or JobStatus.Processing => QueuedBrush,
                    JobStatus.Paused => PausedBrush,
                    JobStatus.Failed or JobStatus.AnalysisFailed => FailedBrush,
                    JobStatus.Completed => CompletedBrush,
                    _ => DefaultBrush
                };
            }
            return DefaultBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    public class StatusToBadgeBgBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush DownloadingBg = new(Color.FromArgb(0x33, 0x00, 0xce, 0xc9));
        private static readonly SolidColorBrush QueuedBg = new(Color.FromArgb(0x33, 0x74, 0xb9, 0xff));
        private static readonly SolidColorBrush PausedBg = new(Color.FromArgb(0x33, 0xfd, 0xcb, 0x6e));
        private static readonly SolidColorBrush FailedBg = new(Color.FromArgb(0x33, 0xff, 0x76, 0x75));
        private static readonly SolidColorBrush CompletedBg = new(Color.FromArgb(0x33, 0x00, 0xb8, 0x94));
        private static readonly SolidColorBrush DefaultBg = new(Color.FromArgb(0x22, 0xff, 0xff, 0xff));

        static StatusToBadgeBgBrushConverter()
        {
            DownloadingBg.Freeze();
            QueuedBg.Freeze();
            PausedBg.Freeze();
            FailedBg.Freeze();
            CompletedBg.Freeze();
            DefaultBg.Freeze();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is JobStatus status)
            {
                return status switch
                {
                    JobStatus.Downloading => DownloadingBg,
                    JobStatus.Queued or JobStatus.PendingAnalysis or JobStatus.Processing => QueuedBg,
                    JobStatus.Paused => PausedBg,
                    JobStatus.Failed or JobStatus.AnalysisFailed => FailedBg,
                    JobStatus.Completed => CompletedBg,
                    _ => DefaultBg
                };
            }
            return DefaultBg;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    public class StatusToPauseVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is JobStatus status)
            {
                return (status == JobStatus.Downloading || status == JobStatus.Queued || status == JobStatus.Processing)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    public class StatusToResumeVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is JobStatus status)
            {
                return status == JobStatus.Paused ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }

    public class StringNotEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
}
