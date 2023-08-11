using BL3Tools;
using BL3Tools.GameData.Items;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace BL3SaveEditor.Helpers
{
    public class SearchVisibilityConverter : IValueConverter
    {
        public string Search { get; set; }


        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {

            if (string.IsNullOrWhiteSpace(Search))
                return true;
            var result = value.ToString().ToLower().Contains(Search) || (value is ItemInfo info && info.SubEffect != null && info.SubEffect.ToLower().Contains(Search));
            if (targetType == typeof(double))
                return result ? 1.0 : 0.0;
            else
                return result ? Visibility.Visible: Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
