using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public class ExportableProperty
    {
        public Type DeclaringType { get; }

        public string Name { get; }

        public object Value { get; }

        public ExportableProperty( Type t, string n, object v )
        {
            DeclaringType = t;
            Name = n;
            Value = v;
        }
    }
}
