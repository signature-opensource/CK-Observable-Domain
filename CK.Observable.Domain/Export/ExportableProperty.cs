using System;

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
