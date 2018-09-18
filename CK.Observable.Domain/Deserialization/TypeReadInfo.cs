using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public class TypeReadInfo
    {
        /// <summary>
        /// Gets the assembly qualified name of the type.
        /// </summary>
        public string AssemblyQualifiedName { get; }

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
                    _localType = SimpleTypeFinder.WeakResolver( AssemblyQualifiedName, false );
                }
                return _localType;
            }
        }

        /// <summary>
        /// Gets the deserialization driver if it can be resolved, null otherwise.
        /// </summary>
        public IDeserializationDriver DeserializationDriver
        {
            get
            {
                if( !_driverLookupDone )
                {
                    _driverLookupDone = true;
                    _driver = UnifiedTypeRegistry.FindDeserializationDriver( AssemblyQualifiedName, () => LocalType );
                }
                return _driver;
            }
        }

        TypeReadInfo _baseType;
        TypeReadInfo[] _typePath;
        Type _localType;
        IDeserializationDriver _driver;
        bool _localTypeLookupDone;
        bool _driverLookupDone;

        internal TypeReadInfo( string t, int version, bool isTrackedObject )
        {
            AssemblyQualifiedName = t;
            Version = version;
            IsTrackedObject = isTrackedObject;
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
