namespace CK.Observable.League.Tests.MicroMachine
{
    [SerializationVersion( 0 )]
    public class Thing : MachineThing
    {
        public Thing( SpecializedMachine m, int tempId )
            : base( m, tempId )
        {
        }

        protected Thing( RevertSerialization _ ) : base( _ ) { }

        Thing( IBinaryDeserializer r, TypeReadInfo? info )
                : base( RevertSerialization.Default )
        {
        }

        void Write( BinarySerializer w )
        {
        }
    }

}
