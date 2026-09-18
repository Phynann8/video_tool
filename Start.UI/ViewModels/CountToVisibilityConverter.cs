using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Start.UI.ViewModels
{
    /// <summary>
    /// Returns Visible when the integer value > 0, Collapsed otherwise.
    /// Used to show/hide the quality ComboBox based on AvailableQualities.Count.
    /// </summary>
    public class CountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
                return count > 0 ? Visibility.Visible : Visibility.Collapsed;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
