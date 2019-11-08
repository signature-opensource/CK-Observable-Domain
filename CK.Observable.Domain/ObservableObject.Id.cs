using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace CK.Observable
{
    public abstract partial class ObservableObject
    {
        /// <summary>
        /// Defines the <see cref="ObservableObject"/> identifier.
        /// </summary>
        [StructLayout( LayoutKind.Explicit )]
        public readonly struct Id
        {
            /// <summary>
            /// A disposed marker value.
            /// </summary>
            static public readonly Id Disposed = new Id( false );

            /// <summary>
            /// An invalid marker value.
            /// </summary>
            static public readonly Id Invalid = new Id( true );

            /// <summary>
            /// The maximal identifier value.
            /// This is guaranteed to roundtrip via <see cref="double"/>.
            /// </summary>
            public const long MaxValue = (long)1 << 53;

            /// <summary>
            /// The maximal index value.
            /// </summary>
            public const int MaxIndexValue = int.MaxValue;

            /// <summary>
            /// The maximal Uniquifier value.
            /// </summary>
            public const int MaxUniquifierValue = (1 << (53 - 32));

            [FieldOffset( 0 )]
            public readonly long UniqueId;
            [FieldOffset( 0 )]
            public readonly int Index;
            [FieldOffset( 4 )]
            public readonly int Uniquifier;

            Id( bool special )
            {
                Index = Uniquifier = 0;
                UniqueId = special ? -1 : -2;
            }

            /// <summary>
            /// Initializes a new Id from an index and a uniquifier.
            /// </summary>
            /// <param name="idx">The index. Must not be negative nor greater than <see cref="MaxIndexValue"/>.</param>
            /// <param name="uniquifier">The uniquifier. Must not be negative nor greater than <see cref="MaxUniquifierValue"/>.</param>
            public Id( int idx, int uniquifier )
            {
                if( idx < 0 ) throw new ArgumentOutOfRangeException( nameof( idx ) );
                if( uniquifier < 0 || uniquifier > MaxUniquifierValue ) throw new ArgumentOutOfRangeException( nameof( uniquifier ) );
                UniqueId = 0;
                Index = idx;
                Uniquifier = uniquifier;
                Debug.Assert( UniqueId <= MaxValue && !Disposed.IsValid && !Invalid.IsValid );
            }

            /// <summary>
            /// Initializes a new Id from its long identifier.
            /// </summary>
            /// <param name="id">The identifier. Must not be negative nor greater than <see cref="MaxValue"/>.</param>
            public Id( long id )
            {
                if( id < 0 || id > MaxValue ) throw new ArgumentOutOfRangeException( nameof( id ) );
                Index = Uniquifier = 0;
                UniqueId = id;
            }

            /// <summary>
            /// Initializes a new Id from a binary stream.
            /// </summary>
            /// <param name="reader">The reader.</param>
            public Id( ICKBinaryReader reader )
                : this( reader.ReadInt64() )
            {
            }

            /// <summary>
            /// Gets whether this identifier is valid: it is not <see cref="Disposed"/> nor <see cref="Invalid"/>.
            /// </summary>
            public bool IsValid => UniqueId >= 0;

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
            public override bool Equals( object obj ) => obj is Id o && o.UniqueId == UniqueId; 

            /// <summary>
            /// Overidden to return <see cref="Uniquifier"/>:<see cref="Index"/>.
            /// </summary>
            /// <returns>A readable string.</returns>
            public override string ToString() => $"{Uniquifier.ToString()}:{Index.ToString()}";

            /// <summary>
            /// Provides the next uniquifier based on a current one.
            /// </summary>
            /// <param name="uniquifier">The current uniquifier.</param>
            /// <returns>The next uniquifier.</returns>
            public static int ForwardUniquifier( int uniquifier )
            {
                if( uniquifier < 0 || uniquifier > MaxUniquifierValue ) throw new ArgumentOutOfRangeException( nameof( uniquifier ) );
                if( ++uniquifier > MaxUniquifierValue ) uniquifier = 0;
                return uniquifier;
            }

            public static bool operator ==( Id id1, Id id2 ) => id1.UniqueId == id2.UniqueId;

            public static bool operator !=( Id id1, Id id2 ) => id1.UniqueId != id2.UniqueId;
        }


    }
}
