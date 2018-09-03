using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    [AttributeUsage(AttributeTargets.Property|AttributeTargets.Class|AttributeTargets.Struct, AllowMultiple = false, Inherited = true)]
    public class NotExportableAttribute : Attribute
    {
    }
}
