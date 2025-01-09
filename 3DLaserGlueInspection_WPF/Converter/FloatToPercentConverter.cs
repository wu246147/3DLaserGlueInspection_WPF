using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace RAIVASCS.Converter
{
    public class FloatToPercentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is float floatvalue)
            {
                floatvalue *= 100;
                return floatvalue.ToString("F2") + "%";
            }
            else if (value is double doublevalue)
            {
                doublevalue *= 100;
                return doublevalue.ToString("F2") + "%";
            }
            return "0%";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
