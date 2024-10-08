using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable;

/// <summary>
/// Specific exception useful to detect race condition for domains.
/// </summary>
public class ObservableDomainDisposedException : ObjectDisposedException
{
    /// <summary>
    /// Initializes a new <see cref="ObservableDomainDisposedException"/>.
    /// </summary>
    /// <param name="domainName">The domain name.</param>
    public ObservableDomainDisposedException( string domainName )
        : base( $"Observable domain '{domainName}'" )
    {
        DomainName = domainName;
    }

    /// <summary>
    /// Gets the domain name.
    /// </summary>
    public string DomainName { get; }
}
