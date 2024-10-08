using CK.BinarySerialization;
using CK.Core;
using CK.Testing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable.Device.Tests;

static class TestHelperExtensions
{
    /// <summary>
    /// Saves this domain in a memory stream and calls <see cref="ObservableDomain.Load(IActivityMonitor, RewindableStream, int, bool?)"/>
    /// that reloads the same domain instance.
    /// </summary>
    /// <param name="domain">The domain to reload.</param>
    public static void Reload( this IMonitorTestHelper @this, ObservableDomain domain )
    {
        using( var s = new MemoryStream() )
        {
            domain.Save( @this.Monitor, s, true );
            s.Position = 0;
            using( var read = RewindableStream.FromStream( s ) )
            {
                domain.Load( @this.Monitor, read );
            }
        }
    }

}
