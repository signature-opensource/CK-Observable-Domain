using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace CK.Observable.Domain.Tests.Serialization
{

    [TestFixture]
    public class DeserializationConstructorTests
    {

        public class Base : IDisposableObject
        {
            ObservableEventHandler<ObservableDomainEventArgs> _disposed;

            public bool IsDisposed { get; private set; }

            public event SafeEventHandler<ObservableDomainEventArgs> Disposed
            {
                add => _disposed.Add( value, nameof( Disposed ) );
                remove => _disposed.Remove( value );
            }

            public Base()
            {
                _disposed = new ObservableEventHandler<ObservableDomainEventArgs>();
            }

            protected Base( RevertSerialization _ ) { }

            public void Dispose()
            {
                if( !IsDisposed )
                {
                    IsDisposed = true;
                    _disposed.Raise( this, null! );
                }
            }

            Base( IBinaryDeserializer r, TypeReadInfo? info )
            {
                if( r.ReadBoolean() )
                {
                    _disposed = new ObservableEventHandler<ObservableDomainEventArgs>( r );
                }
                else IsDisposed = true;
            }

            void Write( BinarySerializer w )
            {
                if( IsDisposed )
                {
                    w.Write( false );
                }
                else
                {
                    w.Write( true );
                    _disposed.Write( w );
                }
            }
        }

        public class Spec : Base
        {
            public Spec( string name )
            {
                Name = name;
            }

            protected Spec( RevertSerialization _ ) : base( _ ) { }

            Spec( IBinaryDeserializer r, TypeReadInfo? info )
                : base( RevertSerialization.Default )
            {
                Debug.Assert( !IsDisposed );
                Name = r.ReadString();
            }

            void Write( BinarySerializer w )
            {
                Debug.Assert( !IsDisposed );
                w.Write( Name );
            }

            public string Name { get; }
        }

        public class Spec2 : Spec
        {
            public Spec2( string name, int power )
                : base( name )
            {
                Power = power;
            }

            Spec2( IBinaryDeserializer r, TypeReadInfo? info )
                : base( RevertSerialization.Default )
            {
                Debug.Assert( !IsDisposed );
                Power = r.ReadInt32();
            }

            void Write( BinarySerializer w )
            {
                Debug.Assert( !IsDisposed );
                w.Write( Power );
            }

            public int Power { get; }
        }


        [Test]
        public void how_it_works()
        {
            var obj = new Spec2( "Reversed", 3712 );

            Spec2 obj2 = SerializeAndDeserialize( obj );
            obj2.Should().NotBeNull();
            obj2.IsDisposed.Should().BeFalse();
            obj2.Power.Should().Be( 3712 );
            obj2.Name.Should().Be( "Reversed" );

            obj.Dispose();

            Spec2 objD = SerializeAndDeserialize( obj );
            objD.Should().NotBeNull();
            objD.IsDisposed.Should().BeTrue();
            objD.Power.Should().Be( 0 );
            objD.Name.Should().BeNull();
        }

        static Spec2 SerializeAndDeserialize( Spec2 obj )
        {
            using var memory = new MemoryStream();
            using( var w = new BinarySerializer( memory, leaveOpen: true ) )
            {
                var writer0 = typeof( Base ).GetMethod( "Write", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly );
                var writer1 = typeof( Spec ).GetMethod( "Write", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly );
                var writer2 = typeof( Spec2 ).GetMethod( "Write", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly );
                var callParams = new object[] { w };
                writer0.Invoke( obj, callParams );
                if( !obj.IsDisposed )
                {
                    writer1.Invoke( obj, callParams );
                    writer2.Invoke( obj, callParams );
                }
            }
            memory.Position = 0;
            object o;
            using( var r = new BinaryDeserializer( memory, leaveOpen: true ) )
            {
                var ctorParamTypes = new Type[] { typeof( IBinaryDeserializer ), typeof( TypeReadInfo ) };
                var ctor0 = typeof( Base ).GetConstructor( BindingFlags.Instance | BindingFlags.NonPublic, null, ctorParamTypes, null );
                var ctor1 = typeof( Spec ).GetConstructor( BindingFlags.Instance | BindingFlags.NonPublic, null, ctorParamTypes, null );
                var ctor2 = typeof( Spec2 ).GetConstructor( BindingFlags.Instance | BindingFlags.NonPublic, null, ctorParamTypes, null );
                var callParams = new object[] { r, null };

                o = System.Runtime.Serialization.FormatterServices.GetUninitializedObject( typeof( Spec2 ) );
                ctor0.Invoke( o, callParams );
                if( !(o is IDisposableObject d) || !d.IsDisposed )
                {
                    ctor1.Invoke( o, callParams );
                    ctor2.Invoke( o, callParams );
                }
            }
            return (Spec2)o;
        }
    }
}
