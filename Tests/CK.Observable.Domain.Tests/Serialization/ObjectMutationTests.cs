//using FluentAssertions;
//using NUnit.Framework;
//using System;
//using System.Collections.Generic;
//using System.Text;
//using static CK.Testing.MonitorTestHelper;

//namespace CK.Observable.Domain.Tests.Serialization
//{
//    [TestFixture]
//    public class ObjectMutationTests
//    {
//        [SerializationVersion(1)]
//        public class Buggy1 : ObservableObject
//        {
//            readonly ObservableList<ObservableObject> _listV0;
//            readonly OwningList<ObservableObject> _listV1;
//            readonly bool _forceV0;


//            public Buggy1( bool forceV0 )
//            {
//                _forceV0 = forceV0;
//                if( _forceV0 ) _listV0 = new ObservableList<ObservableObject>();
//                else _listV1 = new OwningList<ObservableObject>();
//            }

//            Buggy1( IBinaryDeserializer r, TypeReadInfo info )
//                : base( RevertSerialization.Default )
//            {
//                bool v0 = r.ReadBoolean();
//                if( v0 )
//                {
//                    // Possible: a conversion function.
//                    _listV1 = (OwningList<ObservableObject>)r.ReadObject( listV0 => new OwningList( listV0 ) );
//                    // Possible: conversion function is a call to the "mutation" constructor.
//                    _listV1 = (OwningList<ObservableObject>)r.ReadObject(  listV0 => new OwningList( listV0 ) );
//                }
//                else
//                {
//                    _listV1 = (OwningList<ObservableObject>)r.ReadObject();
//                }
//            }

//            void Write( BinarySerializer w )
//            {
//                w.Write( _forceV0 );
//                if( _forceV0 ) w.WriteObject( _listV0 );
//                else w.WriteObject( _listV1 );
//            }
//        }

//        [Test]
//        public void new_object_is_forbidden_while_deserializing()
//        {
//            using var od = new ObservableDomain( TestHelper.Monitor, nameof( new_object_is_forbidden_while_deserializing ), false );
//            od.Modify( TestHelper.Monitor, () =>
//            {
//                new Buggy1( true );
//                new ObservableSet<string>();
//                new ObservableSet<int>();
//            } );

//            ObservableDomain.IdempotenceSerializationCheck( TestHelper.Monitor, od );
//        }

//    }
//}
