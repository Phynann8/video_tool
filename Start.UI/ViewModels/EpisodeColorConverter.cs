using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Start.UI.ViewModels
{
    public class EpisodeColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isSelected)
            {
                return isSelected 
                    ? (Color)ColorConverter.ConvertFromString("#ff0048") 
                    : (Color)ColorConverter.ConvertFromString("#2b2b3d");
            }
            return (Color)ColorConverter.ConvertFromString("#2b2b3d");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
