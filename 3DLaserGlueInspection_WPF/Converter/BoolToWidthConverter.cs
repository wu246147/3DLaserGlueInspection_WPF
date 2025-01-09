using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace RAIVASCS.Converter
{
    public class BoolToWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isVisible && isVisible)
            {
                return new GridLength(1, GridUnitType.Star); // 显示时，列宽为 *，即占用空间
            }
            return new GridLength(0); // 隐藏时，列宽为 0
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
