namespace CK.Observable
{
    /// <summary>
    /// The reader to use obtained from <see cref="IBinaryDeserializerContext.StartReading"/>.
    /// </summary>
    public readonly struct CtorReader
    {
        /// <summary>
        /// Gets the deserializer.
        /// </summary>
        public IBinaryDeserializer Reader { get; }

        /// <summary>
        /// Gets the type based information as it has been written.
        /// If the object has been written by an external driver, this is null.
        /// </summary>
        public TypeReadInfo? Info { get; }

        /// <summary>
        /// Deconstructs this struct.
        /// </summary>
        /// <param name="reader">The <see cref="Reader"/>.</param>
        /// <param name="info">The <see cref="Info"/>.</param>
        public void Deconstruct( out IBinaryDeserializer reader, out TypeReadInfo? info )
        {
            reader = Reader;
            info = Info;
        }

        internal CtorReader( IBinaryDeserializer d, TypeReadInfo? info )
        {
            Reader = d;
            Info = info;
        }
    }
}
