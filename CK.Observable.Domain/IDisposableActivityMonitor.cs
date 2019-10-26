using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Core
{
    public interface IDisposableActivityMonitor : IActivityMonitor, IDisposable
    {
    }
}
