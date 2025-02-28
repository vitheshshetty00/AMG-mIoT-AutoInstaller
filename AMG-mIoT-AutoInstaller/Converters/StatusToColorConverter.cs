using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AMG_mIoT_AutoInstaller.Converters
{
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                return status.ToLower() switch
                {
                    "completed" => new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#10B981")
                    ), // Green
                    "failed" => new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#EF4444")
                    ), // Red
                    "installing..." => new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#3B82F6")
                    ), // Blue
                    _ => new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#6B7280")
                    ) // Gray
                    ,
                };
            }
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B7280"));
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture
        )
        {
            throw new NotImplementedException();
        }
    }
}
