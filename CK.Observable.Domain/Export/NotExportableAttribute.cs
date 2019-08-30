using System;

namespace CK.Observable
{
    [AttributeUsage(AttributeTargets.Property|AttributeTargets.Class|AttributeTargets.Struct, AllowMultiple = false, Inherited = true)]
    public class NotExportableAttribute : Attribute
    {
    }
}
