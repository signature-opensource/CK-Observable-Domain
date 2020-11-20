using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CK.Observable
{
    /// <summary>
    /// Descriptor for a potential <see cref="Type"/>.
    /// </summary>
    public class TypeReadInfo
    {
        /// <summary>
        /// Gets the serialized type name. It's the assembly qualified name.
        /// </summary>
        public string TypeName { get; }

        /// <summary>
        /// Gets the simplified <see cref="TypeName"/>.
        /// </summary>
        public string SimpleTypeName => TypeName.Split( ',' )[0];

        /// <summary>
        /// Gets whether the object is tracked (reference type) or not (value type).
        /// </summary>
        public bool IsTrackedObject { get; }

        /// <summary>
        /// Gets the version (greater or equal to 0) if this type information has been serialized
        /// by the type itself. -1 otherwise.
        /// </summary>
        public readonly int Version;

        /// <summary>
        /// Gets the base type infromation.
        /// Not null only if this type information has been serialized by the type itself and if the
        /// type was a specialized class.
        /// </summary>
        public TypeReadInfo BaseType => _baseType;

        /// <summary>
        /// Gets the types from the root inherited type (excluding Object) down to this one.
        /// Not null only if this type information has been serialized by the type itself.
        /// When not null, the list ends with this <see cref="TypeReadInfo"/> itself.
        /// </summary>
        public IReadOnlyList<TypeReadInfo> TypePath => _typePath;

        /// <summary>
        /// Gets the Type if it can be resolved locally, null otherwise.
        /// </summary>
        public Type LocalType
        {
            get
            {
                if( !_localTypeLookupDone )
                {
                    _localTypeLookupDone = true;
                    _localType = SimpleTypeFinder.WeakResolver( TypeName, false );
                }
                return _localType;
            }
        }

        /// <summary>
        /// Gets the deserialization driver if it can be resolved, null otherwise.
        /// </summary>
        /// <param name="r">The deserializer.</param>
        public IDeserializationDriver GetDeserializationDriver( IDeserializerResolver r )
        {
            if( !_driverLookupDone )
            {
                _driverLookupDone = true;
                _driver = r.FindDriver( TypeName, () => LocalType );
            }
            return _driver;
        }

        TypeReadInfo _baseType;
        TypeReadInfo[] _typePath;
        Type _localType;
        IDeserializationDriver _driver;
        bool _localTypeLookupDone;
        bool _driverLookupDone;

        internal TypeReadInfo( string t, int version, bool isTrackedObject )
        {
            TypeName = t;
            Version = version;
            IsTrackedObject = isTrackedObject;
        }

        /// <summary>
        /// Constructor for Object type, bound to <see cref="BasicTypeDrivers.DObject"/>.
        /// </summary>
        /// <param name="r">The resolver.</param>
        internal TypeReadInfo( IDeserializerResolver r )
        {
            _localType = typeof( object );
            _typePath = new[] { this };
            TypeName = String.Empty;
            Version = 0;
            IsTrackedObject = true;
            _localTypeLookupDone = true;
            _driverLookupDone = true;
            _driver = r.FindDriver( _localType.AssemblyQualifiedName );
        }

        internal void SetBaseType( TypeReadInfo b )
        {
            _baseType = b;
        }

        internal TypeReadInfo[] EnsureTypePath()
        {
            Debug.Assert( Version >= 0, "Must be called only for TypeBased serialization." );
            if( _typePath == null )
            {
                if( _baseType != null )
                {
                    var basePath = _baseType.EnsureTypePath();
                    var p = new TypeReadInfo[basePath.Length + 1];
                    Array.Copy( basePath, p, basePath.Length );
                    p[basePath.Length] = this;
                    _typePath = p;
                }
                else _typePath = new[] { this };
            }
            return _typePath;
        }
    }

}
