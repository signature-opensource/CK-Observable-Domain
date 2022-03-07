using CK.Core;

namespace CK.Observable.League.Tests.MicroMachine
{
    [SerializationVersion( 0 )]
    public class Thing : MachineThing
    {
        public Thing( SpecializedMachine m, int tempId )
            : base( m, tempId )
        {
        }

        protected Thing( BinarySerialization.Sliced _ ) : base( _ ) { }

        Thing( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
            : base( BinarySerialization.Sliced.Instance )
        {
        }

        public static void Write( BinarySerialization.IBinarySerializer s, in Thing o )
        {
        }
    }

}
