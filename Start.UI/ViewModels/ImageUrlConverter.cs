using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Start.UI.ViewModels
{
    /// <summary>
    /// Converts a URL string to BitmapImage safely.
    /// Returns DependencyProperty.UnsetValue for null/empty strings so WPF Image.Source
    /// shows nothing instead of throwing a NotSupportedException binding error.
    /// </summary>
    public class ImageUrlConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string url || string.IsNullOrWhiteSpace(url))
                return DependencyProperty.UnsetValue;

            try
            {
                var uri = new Uri(url, UriKind.Absolute);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = uri;
                bitmap.CacheOption = BitmapCacheOption.OnDemand;
                bitmap.EndInit();
                return bitmap;
            }
            catch
            {
                return DependencyProperty.UnsetValue;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
