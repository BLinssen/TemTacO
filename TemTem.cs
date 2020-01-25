using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TemTacO
{
    class TemTem
    {
        public int Number { get; set; }
        public string Name { get; set; }
        public string Type1 { get; set; }
        public string Type2 { get; set; }
        public string Trait { get; set; }
        public float TypeNeutral { get; set; }
        public float TypeFire { get; set; }
        public float TypeWater { get; set; }
        public float TypeNature { get; set; }
        public float TypeElectric { get; set; }
        public float TypeEarth { get; set; }
        public float TypeMental { get; set; }
        public float TypeWind { get; set; }
        public float TypeDigital { get; set; }
        public float TypeMelee { get; set; }
        public float TypeCrystal { get; set; }
        public float TypeToxic { get; set; }

        public static TemTem FromCsv(string csvLine)
        {
            string[] values = csvLine.Split(',');
            TemTem temValues = new TemTem();
            temValues.Number = Convert.ToInt32(values[0]);
            temValues.Name = Convert.ToString(values[1]);
            temValues.Type1 = Convert.ToString(values[2]);
            temValues.Type2 = Convert.ToString(values[3]);
            temValues.Trait = Convert.ToString(values[4]);
            temValues.TypeNeutral = (float)Convert.ToDouble(values[5]);
            temValues.TypeFire = (float)Convert.ToDouble(values[6]);
            temValues.TypeWater = (float)Convert.ToDouble(values[7]);
            temValues.TypeNature = (float)Convert.ToDouble(values[8]);
            temValues.TypeElectric = (float)Convert.ToDouble(values[9]);
            temValues.TypeEarth = (float)Convert.ToDouble(values[10]);
            temValues.TypeMental = (float)Convert.ToDouble(values[11]);
            temValues.TypeWind = (float)Convert.ToDouble(values[12]);
            temValues.TypeDigital = (float)Convert.ToDouble(values[13]);
            temValues.TypeMelee = (float)Convert.ToDouble(values[14]);
            temValues.TypeCrystal = (float)Convert.ToDouble(values[15]);
            temValues.TypeToxic = (float)Convert.ToDouble(values[16]);
            return temValues;
        }
    }
}
