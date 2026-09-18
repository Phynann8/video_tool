using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Start.UI.ViewModels
{
    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                if (targetType == typeof(Visibility))
                {
                    return boolValue ? Visibility.Collapsed : Visibility.Visible;
                }

                return !boolValue;
            }

            return targetType == typeof(Visibility) ? Visibility.Visible : false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return false;
        }
    }
}
