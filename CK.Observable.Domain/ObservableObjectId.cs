using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace CK.Observable
{
    /// <summary>
    /// Defines the <see cref="ObservableObject.OId"/> identifier.
    /// Note that only the <see cref="UniqueId"/> is exported via <see cref="IObjectExporterTarget.EmitInt64(long)"/>
    /// and that this unique identifier is guaranteed to round trip via <see cref="double"/> (only 53 bits are used).
    /// </summary>
    public readonly struct ObservableObjectId
    {
        const int IndexBitCount = 25;

        /// <summary>
        /// A disposed marker value.
        /// </summary>
        static public readonly ObservableObjectId Destroyed = new ObservableObjectId( false );

        /// <summary>
        /// An invalid marker value.
        /// </summary>
        static public readonly ObservableObjectId Invalid = new ObservableObjectId( true );

        /// <summary>
        /// The maximal identifier value is used for the identifier of <see cref="Destroyed"/> special value.
        /// This is guaranteed to roundtrip via <see cref="double"/>.
        /// </summary>
        public const long MaxValue = 1L << 53;

        /// <summary>
        /// The maximal index value.
        /// </summary>
        public const int MaxIndexValue = (1 << IndexBitCount)-1;

        /// <summary>
        /// The maximal Uniquifier value.
        /// </summary>
        public const int MaxUniquifierValue = (1 << (53 - IndexBitCount)) - 1;

        /// <summary>
        /// The full identifier.
        /// </summary>
        public readonly long UniqueId;

        /// <summary>
        /// Gets the index part of this identifier.
        /// </summary>
        public int Index => (int)(UniqueId & MaxIndexValue);

        /// <summary>
        /// Gets the uniquifier part of this identifier.
        /// </summary>
        public int Uniquifier => (int)(UniqueId >> IndexBitCount);

        ObservableObjectId( bool isDisposed )
        {
            UniqueId = isDisposed
                        ? MaxValue
                        : -1L;
        }

        /// <summary>
        /// Initializes a new Id from an index and a uniquifier.
        /// </summary>
        /// <param name="idx">The index. Must not be negative nor greater than <see cref="MaxIndexValue"/>.</param>
        /// <param name="uniquifier">The uniquifier. Must not be negative nor greater than <see cref="MaxUniquifierValue"/>.</param>
        public ObservableObjectId( int idx, int uniquifier )
        {
            if( idx < 0 || idx > MaxIndexValue ) throw new ArgumentOutOfRangeException( nameof( idx ) );
            if( uniquifier < 0 || uniquifier > MaxUniquifierValue ) throw new ArgumentOutOfRangeException( nameof( uniquifier ) );
            UniqueId = (long)(((ulong)uniquifier) << IndexBitCount) | (uint)idx;
            Debug.Assert( UniqueId < MaxValue, "UniqueId < MaxValue" );
            Debug.Assert( IsValid, $"IsValid ({idx},{uniquifier}) => {UniqueId}" );
            Debug.Assert( !Destroyed.IsValid, "!Disposed.IsValid" );
            Debug.Assert( !Invalid.IsValid, "!Invalid.IsValid" );
            Debug.Assert( (((long)MaxUniquifierValue << IndexBitCount) | MaxIndexValue)+1 == MaxValue, "(((long)MaxUniquifierValue << IndexBitCount) | MaxIndexValue)+1 == MaxValue" );
            Debug.Assert( (long)Math.Floor( ((double)MaxValue) ) == MaxValue, "(long)Math.Floor( ((double)MaxValue) ) == MaxValue" );
            Debug.Assert( (long)Math.Floor( ((double)(MaxValue+1)) ) != MaxValue+1, "This is the maximal possible long: (long)Math.Floor( ((double)(MaxValue+1)) ) != MaxValue+1" );
        }

        /// <summary>
        /// Initializes a new Id from its long identifier.
        /// </summary>
        /// <param name="id">The identifier. Must not be negative nor greater than <see cref="MaxValue"/>.</param>
        /// <param name="throwOnInvalid">Whether this must throw an <see cref="ArgumentOutOfRangeException"/> if the id is negative or greater or equal to <see cref="MaxValue"/>.</param>
        public ObservableObjectId( long id, bool throwOnInvalid = true )
        {
            if( throwOnInvalid && (id < 0 || id >= MaxValue) ) throw new ArgumentOutOfRangeException( nameof( id ) );
            UniqueId = id;
        }

        /// <summary>
        /// Initializes a new Id from a binary stream.
        /// </summary>
        /// <param name="r">The reader.</param>
        public ObservableObjectId( ICKBinaryReader r )
        {
            UniqueId = r.ReadInt64();
        }

        void Export( int num, ObjectExporter exporter )
        {
            exporter.Target.EmitInt64( UniqueId );
        }

        /// <summary>
        /// Gets whether this identifier is valid: it is not <see cref="Destroyed"/> nor <see cref="Invalid"/>.
        /// </summary>
        public bool IsValid => UniqueId >= 0 && UniqueId < MaxValue;

        /// <summary>
        /// Writes this Id to a binary stream.
        /// </summary>
        /// <param name="w">The writer.</param>
        public void Write( ICKBinaryWriter w ) => w.Write( UniqueId );

        /// <summary>
        /// The <see cref="Uniquifier"/> does perfectly the job.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode() => Uniquifier;

        /// <summary>
        /// Uses the <see cref="UniqueId"/>.
        /// </summary>
        /// <param name="obj">The other object to compare to.</param>
        /// <returns>True if equal, false otherwise.</returns>
        public override bool Equals( object obj ) => obj is ObservableObjectId o && o.UniqueId == UniqueId;

        /// <summary>
        /// Overidden to return <see cref="Index"/>.
        /// </summary>
        /// <returns>This object's index.</returns>
        public override string ToString() => Index.ToString();

        /// <summary>
        /// Computes the next uniquifier based on a current one.
        /// </summary>
        /// <param name="uniquifier">The current uniquifier.</param>
        internal static int ForwardUniquifier( ref int uniquifier )
        {
            Debug.Assert( uniquifier >= 0 && uniquifier <= MaxUniquifierValue );
            if( ++uniquifier > MaxUniquifierValue ) uniquifier = 0;
            return uniquifier;
        }

        /// <summary>
        /// Implements equality operator: uses the <see cref="UniqueId"/>.
        /// </summary>
        /// <param name="o1">The first object.</param>
        /// <param name="o2">The second object.</param>
        /// <returns>True if the <see cref="UniqueId"/> are the same.</returns>
        public static bool operator ==( ObservableObjectId o1, ObservableObjectId o2 ) => o1.UniqueId == o2.UniqueId;

        /// <summary>
        /// Implements inequality operator: uses the <see cref="UniqueId"/>.
        /// </summary>
        /// <param name="o1">The first object.</param>
        /// <param name="o2">The second object.</param>
        /// <returns>True if the <see cref="UniqueId"/> are different.</returns>
        public static bool operator !=( ObservableObjectId o1, ObservableObjectId o2 ) => o1.UniqueId != o2.UniqueId;
    }
}
