using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace CK.Observable.Domain
{
    public abstract partial class ObservableObject
    {
        [StructLayout( LayoutKind.Explicit )]
        public readonly struct Id
        {
            /// <summary>
            /// The maximal identifier value.
            /// This is guaranteed to roundtrip via <see cref="double"/>.
            /// </summary>
            public const ulong MaxValue = 1 << 53;

            /// <summary>
            /// The maximal index value.
            /// </summary>
            public const ulong MaxIndexValue = (1 << 25) - 1;

            /// <summary>
            /// The maximal Uniquifier value.
            /// </summary>
            public const ulong MaxUniquifierValue = (1 << (53 - 25));

            [FieldOffset( 0 )]
            public readonly ulong UniqueId;
            [FieldOffset( 0 )]
            public readonly uint Index;
            [FieldOffset( 4 )]
            public readonly uint Uniquifier;

            /// <summary>
            /// Initializes a new Id from an index and a uniquifier.
            /// </summary>
            /// <param name="idx">The index. Must not be greater than <see cref="MaxIndexValue"/>.</param>
            /// <param name="uniquifier">The uniquifier. Must not be greater than <see cref="MaxUniquifierValue"/>.</param>
            public Id( uint idx, uint uniquifier )
            {
                if( idx > MaxIndexValue ) throw new ArgumentOutOfRangeException( nameof( idx ) );
                if( uniquifier > MaxUniquifierValue ) throw new ArgumentOutOfRangeException( nameof( uniquifier ) );
                UniqueId = ((ulong)uniquifier << 25) | idx;
                Index = idx;
                Uniquifier = uniquifier;
                Debug.Assert( UniqueId <= MaxValue );
            }

            /// <summary>
            /// Initializes a new Id from its long identifier.
            /// </summary>
            /// <param name="id">The identifier. Must not be greater than <see cref="MaxValue"/>.</param>
            public Id( ulong id )
            {
                if( id > MaxValue ) throw new ArgumentOutOfRangeException( nameof( id ) );
                UniqueId = id;
                Index = (uint)(id & MaxIndexValue);
                Uniquifier = (uint)(id >> 25);
            }

            /// <summary>
            /// Initializes a new Id from a binary stream.
            /// </summary>
            /// <param name="reader">The reader.</param>
            public Id( ICKBinaryReader reader )
                : this( reader.ReadUInt64() )
            {
            }

            /// <summary>
            /// Writes this Id to a binary stream.
            /// </summary>
            /// <param name="w">The writer.</param>
            public void Write( ICKBinaryWriter w ) => w.Write( UniqueId );

            /// <summary>
            /// The <see cref="Uniquifier"/> does perfectly the job.
            /// </summary>
            /// <returns></returns>
            public override int GetHashCode() => (int)Uniquifier;

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

        }


    }
}
