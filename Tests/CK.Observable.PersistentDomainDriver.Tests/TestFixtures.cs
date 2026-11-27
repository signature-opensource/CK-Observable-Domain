using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CK.Core;
using Microsoft.Extensions.Configuration;

namespace CK.Observable.PersistentDomainDriverTests;

[SerializationVersion( 0 )]
public sealed class OCounter : ObservableObject
{
    public OCounter()
    {
    }

    OCounter( BinarySerialization.IBinaryDeserializer d, BinarySerialization.ITypeReadInfo info )
        : base( BinarySerialization.Sliced.Instance )
    {
        Count = d.Reader.ReadInt32();
    }

    public static void Write( BinarySerialization.IBinarySerializer s, in OCounter o )
    {
        s.Writer.Write( o.Count );
    }

    public int Count { get; set; }
}

[SerializationVersion( 0 )]
public sealed class OTestSingleton : ObservableObject, IObservableDomainSingleton
{
    internal OTestSingleton()
    {
    }

    OTestSingleton( BinarySerialization.IBinaryDeserializer d, BinarySerialization.ITypeReadInfo info )
        : base( BinarySerialization.Sliced.Instance )
    {
        Marker = d.Reader.ReadNullableString();
    }

    public static void Write( BinarySerialization.IBinarySerializer s, in OTestSingleton o )
    {
        s.Writer.WriteNullableString( o.Marker );
    }

    public string? Marker { get; set; }
}

public static class TestStore
{
    public static string NewDirectory()
    {
        var p = Path.Combine( Path.GetTempPath(), "CK.Observable.PersistentDomainDriver.Tests", Guid.NewGuid().ToString( "N" ) );
        Directory.CreateDirectory( p );
        return p;
    }
}

/// <summary>
/// Helpers for the driver tests that work on a folder: options, file lists and file contents.
/// </summary>
public static class DriverTestKit
{
    /// <summary>Gets a service provider with the options of one domain. The folder is the store path.</summary>
    public static IServiceProvider NewServices( string store,
                                                string domainName,
                                                string snapshotSaveDelay = "00:00:00",
                                                Dictionary<string, string?>? extra = null )
    {
        var values = new Dictionary<string, string?>
        {
            [$"CK-ObservableDomain:Domains:{domainName}:StorePath"] = store,
            [$"CK-ObservableDomain:Domains:{domainName}:SnapshotSaveDelay"] = snapshotSaveDelay,
        };
        if( extra != null )
        {
            foreach( var kv in extra ) values[kv.Key] = kv.Value;
        }
        return new SingleServiceProvider( new ConfigurationBuilder().AddInMemoryCollection( values ).Build() );
    }

    /// <summary>Gets the ".od" files of the folder, oldest first.</summary>
    public static string[] OdFiles( string folder ) => Directory.GetFiles( folder, "*.od" ).OrderBy( p => p, StringComparer.Ordinal ).ToArray();

    /// <summary>
    /// Gets the name and the SHA-256 of each file of the folder. The lock file is always empty, and a running driver
    /// holds it: the method does not read it.
    /// </summary>
    public static Dictionary<string, string> Content( string folder )
        => Directory.GetFiles( folder ).ToDictionary( p => Path.GetFileName( p ),
                                                      p => p.EndsWith( ".lock", StringComparison.Ordinal )
                                                            ? "(lock file)"
                                                            : Convert.ToHexString( System.Security.Cryptography.SHA256.HashData( File.ReadAllBytes( p ) ) ) );

    /// <summary>Gets whether two results of <see cref="Content"/> are equal.</summary>
    public static bool SameContent( Dictionary<string, string> a, Dictionary<string, string> b )
        => a.Count == b.Count && a.All( kv => b.TryGetValue( kv.Key, out var v ) && v == kv.Value );

    /// <summary>Describes a result of <see cref="Content"/> for a test message.</summary>
    public static string Describe( Dictionary<string, string> c )
        => string.Join( "; ", c.OrderBy( kv => kv.Key, StringComparer.Ordinal ).Select( kv => $"{kv.Key}={kv.Value.Substring( 0, 8 )}" ) );

    /// <summary>Writes zeros over the whole file. Its length does not change.</summary>
    public static void ZeroFill( string path )
    {
        long length = new FileInfo( path ).Length;
        File.WriteAllBytes( path, new byte[length] );
    }

    /// <summary>Reads a file, checks its format and loads its payload in a new client.</summary>
    public static ObservableDomain LoadFile( IActivityMonitor monitor, string path, string domainName )
    {
        byte[]? payload;
        using( var input = File.OpenRead( path ) )
        {
            if( SnapshotFileFormat.Read( input, out _, out payload ) != SnapshotReadStatus.Valid || payload == null )
            {
                throw new InvalidDataException( $"The file '{path}' is not valid." );
            }
        }
        var client = new PersistentDomainClient( domainName, new PersistentDomainOptions(), null );
        return client.Load( monitor, payload );
    }

    /// <summary>Gets the marker of the <see cref="OTestSingleton"/> in a file of "TestDomain".</summary>
    public static string? MarkerOf( IActivityMonitor monitor, string path )
    {
        using var d = LoadFile( monitor, path, "TestDomain" );
        return d.AllObjects.Items.OfType<OTestSingleton>().Single().Marker;
    }
}
