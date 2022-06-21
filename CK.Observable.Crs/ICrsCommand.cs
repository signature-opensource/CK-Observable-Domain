using CK.Crs.CommandDiscoverer.Attributes;

namespace CK.Crs
{
    /// <summary>
    /// Marker interface that should be defined in CK.Crs.Abstractions but is not.
    /// <para>
    /// The command MUST be decorated with <see cref="CommandNameAttribute"/>.
    /// </para>
    /// <para>
    /// Objects marked with this interface will automatically be sent to 
    /// the <see cref="ICommandDispatcher"/> by the <see cref="CK.Observable.CrsSidekick"/>.
    /// </para>
    /// </summary>
    public interface ICrsCommand
    {
    }
}