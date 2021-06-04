using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;

namespace MangaViewer_Zip
{
    public class SizePercentageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is double param)
            {
                return param * System.Convert.ToDouble(value);
            }
            else if (parameter is string str && double.TryParse(str, out var number))
            {
                return System.Convert.ToDouble(value) * number;
            }
            else
            {
                return 0.8 * System.Convert.ToDouble(value);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Don't need to implement this
            return null;
        }
    }
}
