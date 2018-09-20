using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public interface IKnowMyExportDriver
    {
        IObjectExportTypeDriver ExportDriver { get; }
    }
}
