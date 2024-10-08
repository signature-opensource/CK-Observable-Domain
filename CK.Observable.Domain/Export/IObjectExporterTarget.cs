using System;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace CK.Observable;

public interface IObjectExporterTarget
{
    /// <summary>
    /// Resets any internal state so that any contextual information are lost.
    /// </summary>
    void ResetContext();

    /// <summary>
    /// Emits a null object reference.
    /// </summary>
    void EmitNull();

    /// <summary>
    /// Emits a reference to a an object thanks to its number. 
    /// </summary>
    /// <param name="num">The object number: must be 0 or positive.</param>
    void EmitReference( int num );

    /// <summary>
    /// Emits an empty object that can be referenced.
    /// </summary>
    /// <param name="num">The object number: must be 0 or positive.</param>
    void EmitEmptyObject( int num );

    /// <summary>
    /// Emits a string value.
    /// </summary>
    /// <param name="value">The string value.</param>
    void EmitString( string value );

    /// <summary>
    /// Emits the start of an object that may be one of <see cref="ObjectExportedKind"/>.
    /// </summary>
    /// <param name="num">The object that can be negative: in such case, the object cannot be referenced.</param>
    /// <param name="kind">The object kind.</param>
    void EmitStartObject( int num, ObjectExportedKind kind );

    /// <summary>
    /// Emits a property name.
    /// </summary>
    /// <param name="name">The name. Must not be null or empty.</param>
    void EmitPropertyName( string name );

    /// <summary>
    /// Closes an object previously opened by <see cref="EmitStartObject(int, ObjectExportedKind)"/>.
    /// </summary>
    /// <param name="num">The object number (negative for an object that cannot be referenced).</param>
    /// <param name="kind">The object kind.</param>
    void EmitEndObject( int num, ObjectExportedKind kind );

    /// <summary>
    /// Emits a double.
    /// </summary>
    /// <param name="o">The value to emit.</param>
    void EmitDouble( double o );

    /// <summary>
    /// Emits a single.
    /// </summary>
    /// <param name="o">The value to emit.</param>
    void EmitSingle( float o );

    /// <summary>
    /// Emits a boolean value.
    /// </summary>
    /// <param name="o">The value to emit.</param>
    void EmitBool( bool o );

    void EmitChar( char o );
    void EmitSByte( decimal o );
    void EmitInt16( short o );
    void EmitByte( byte o );
    void EmitUInt16( ushort o );
    void EmitInt32( int o );
    void EmitUInt32( uint o );
    void EmitInt64( long o );
    void EmitUInt64( ulong o );
    void EmitGuid( Guid o );
    void EmitDateTime( DateTime o );
    void EmitTimeSpan( TimeSpan o );
    void EmitDateTimeOffset( DateTimeOffset o );
    void EmitStartList();
    void EmitEndList();
}
