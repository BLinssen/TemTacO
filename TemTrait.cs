using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TemTacO
{
    class TemTrait
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public static TemTrait FromCsv(string csvLine, CultureInfo cultureInfo)
        {
            string[] values = csvLine.Split(';');
            TemTrait temTraits = new TemTrait();
            temTraits.Name = Convert.ToString(values[0], cultureInfo);
            temTraits.Description = Convert.ToString(values[1], cultureInfo);
            return temTraits;
        }
    }
}
