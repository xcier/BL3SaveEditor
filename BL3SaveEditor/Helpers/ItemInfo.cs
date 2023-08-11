using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BL3SaveEditor.Helpers
{
    public class ItemInfo
    {
        public string Part { get; set; }
        public string Positives { get; set; }
        public string Negatives { get; set; }
        public string Effects { get; set; }
        
        private string _subEffect;
        public string SubEffect => _subEffect ?? GetSubEffect();

        private string GetSubEffect()
        {
            string str = null;
            if(Negatives != null)
            {
                if(Positives != null)
                {
                    str = $"{Positives}, {Negatives}";
                }
                else
                {
                    str = Negatives;
                }
            }
            else if(Positives != null)
            {
                str = Positives;
            }
            if(str != null)
            {
                if (Effects != null)
                {
                    str = $"{Effects}\n{str}";
                }
            }
            else if(Effects != null)
            {
                str = Effects;
            }
            _subEffect = str;
            return _subEffect;
        }

        public override string ToString()
        {
            return Part;
        }
    }
}
