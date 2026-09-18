using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Start.UI.ViewModels
{
    /// <summary>
    /// Converts (Progress 0-100, ContainerWidth) → pixel width for the green fill layer.
    /// The purple track Border is always full-width; this inner Border grows from the left.
    /// </summary>
    public class ProgressWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 2
                && values[0] is double progress
                && values[1] is double containerWidth
                && containerWidth > 0)
            {
                return Math.Max(0, Math.Min(containerWidth, containerWidth * progress / 100d));
            }
            return 0d;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Returns green for Downloading/Completed, orange for Queued, red for Failed/Cancelled.
    /// Used to tint the fill layer of the progress bar.
    /// </summary>
    public class JobStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Start.Core.Models.JobStatus status)
            {
                return status switch
                {
                    Start.Core.Models.JobStatus.Completed  => "#00b894",
                    Start.Core.Models.JobStatus.Failed     => "#d63031",
                    Start.Core.Models.JobStatus.Cancelled  => "#636e72",
                    Start.Core.Models.JobStatus.Queued     => "#fdcb6e",
                    _                                      => "#00cec9"   // Downloading / Processing
                };
            }
            return "#00cec9";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Returns Visible only when the job is actively Downloading or Queued (so the ✕ button shows).
    /// </summary>
    public class JobStatusToStopVisConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Start.Core.Models.JobStatus status)
            {
                return (status == Start.Core.Models.JobStatus.Downloading
                     || status == Start.Core.Models.JobStatus.Queued
                     || status == Start.Core.Models.JobStatus.Processing)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
