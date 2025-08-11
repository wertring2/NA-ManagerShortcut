using System;
using System.Globalization;
using System.Windows.Data;

namespace NA_ManagerShortcut.Converters
{
    public class BoolToHideTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isHidden)
            {
                return isHidden ? "Unhide Adapter" : "Hide Adapter";
            }
            return "Hide Adapter";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}