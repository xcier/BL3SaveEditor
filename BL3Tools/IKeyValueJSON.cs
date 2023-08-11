using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BL3Tools
{
    internal interface IKeyValueJSON<TKey, TValue>
    {
        public TKey key { get; set; }
        public TValue value { get; set; }
    }
}
