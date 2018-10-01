using CK.Core;
using CK.Observable;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Signature.Process.Dispatching
{
    [SerializationVersion( 0 )]
    [DebuggerDisplay( "NotificationState" )]
    public class NotificationState : ObservableRootObject
    {
        private readonly ObservableList<Notification<DispatchProductResult>> _productDispatchErrors;
        public IReadOnlyList<Notification<DispatchProductResult>> ProductDispatchErrors => _productDispatchErrors;

        private readonly ObservableList<Notification<CKExceptionData>> _exceptions;
        public IReadOnlyList<Notification<CKExceptionData>> Exceptions => _exceptions;

        public BarcodeScannerState BarcodeScanner { get; }

        public NotificationState( ObservableDomain domain ) : base( domain )
        {
            _productDispatchErrors = new ObservableList<Notification<DispatchProductResult>>();
            _exceptions = new ObservableList<Notification<CKExceptionData>>();
            BarcodeScanner = new BarcodeScannerState();
        }

        protected NotificationState( IBinaryDeserializerContext d ) : base( d )
        {
            var r = d.StartReading();
            if( r.CurrentReadInfo.Version != 0 ) throw new NotSupportedException( $"Cannot deserialize {nameof( NotificationState )} version {r.CurrentReadInfo.Version}" );

            _productDispatchErrors = (ObservableList<Notification<DispatchProductResult>>)r.ReadObject();
            _exceptions = (ObservableList<Notification<CKExceptionData>>)r.ReadObject();
            BarcodeScanner = (BarcodeScannerState)r.ReadObject();
        }

        void Write( BinarySerializer w )
        {
            w.WriteObject( _productDispatchErrors );
            w.WriteObject( _exceptions );
            w.WriteObject( BarcodeScanner );
        }

        public void HandleDispatchResult( DispatchProductResult r )
        {
            if( !r.Success )
            {
                _productDispatchErrors.Add( new Notification<DispatchProductResult>( r ) );
            }
        }

        public void AddException( CKExceptionData exceptionData )
        {
            _exceptions.Add( new Notification<CKExceptionData>( exceptionData ) );
        }

        public void ClearNotification( string notificationId )
        {
            var productDispatchError = _productDispatchErrors.FirstOrDefault( n => n.Id == notificationId );
            if( productDispatchError != null )
            {
                _productDispatchErrors.Remove( productDispatchError );
            }
            var exception = _exceptions.FirstOrDefault( n => n.Id == notificationId );
            if( exception != null )
            {
                _exceptions.Remove( exception );
            }
        }

    }
    public class Notification<T> : ObservableObject
    {
        public Notification( T body )
        {
            Body = body;
        }
        public string Id { get; } = Guid.NewGuid().ToString( "N" );
        public T Body { get; }
    }

    public interface IBarcodeScannerState
    {
        DateTime LastSeen { get; }
        string LastScanIdentifier { get; }
        IReadOnlyDictionary<string, string> LastScanMetadata { get; }
        void UpdateOnScan( string productIdentifier, IReadOnlyDictionary<string, string> metadata );
    }

    [SerializationVersion( 0 )]
    [DebuggerDisplay( "BarcodeScannerState" )]
    public class BarcodeScannerState : ObservableObject, IBarcodeScannerState
    {
        public DateTime LastSeen { get; private set; }
        public string LastScanIdentifier { get; private set; }
        public ObservableDictionary<string, string> LastScanMetadata { get; }

        IReadOnlyDictionary<string, string> IBarcodeScannerState.LastScanMetadata => LastScanMetadata;

        public BarcodeScannerState()
        {
            LastScanMetadata = new ObservableDictionary<string, string>();
        }

        protected BarcodeScannerState( IBinaryDeserializerContext d ) : base( d )
        {
            var r = d.StartReading();
            LastSeen = r.ReadDateTime();
            LastScanIdentifier = r.ReadNullableString();
            LastScanMetadata = (ObservableDictionary<string, string>)r.ReadObject();
        }

        void Write( BinarySerializer w )
        {
            w.Write( LastSeen );
            w.WriteNullableString( LastScanIdentifier );
            w.WriteObject( LastScanMetadata );
        }

        private void UpdateLastSeen()
        {
            LastSeen = DateTime.UtcNow;
        }

        public void UpdateOnScan( string productIdentifier, IReadOnlyDictionary<string, string> metadata )
        {
            UpdateLastSeen();
            LastScanIdentifier = productIdentifier;
            LastScanMetadata.Clear();
            if( metadata != null )
            {
                LastScanMetadata.AddRange( metadata );
            }
        }

        public class DispatchProductResult
        {
            public DispatchProductResultType DispatchProductResultType { get; }
            public string ProductId { get; }
            public ObservableList<string> OrdersWithProduct { get; }

            public DispatchProductResult( DispatchProductResultType dispatchProductResultType, string productId = null, IEnumerable<string> ordersWithProduct = null )
            {
                DispatchProductResultType = dispatchProductResultType;
                ProductId = productId;
                if( ordersWithProduct != null )
                {
                    OrdersWithProduct = new ObservableList<string>();
                    OrdersWithProduct.AddRange( ordersWithProduct );
                }
            }

            public bool Success
                => DispatchProductResultType == DispatchProductResultType.Success;
        }

        public enum DispatchProductResultType
        {
            Success = 0,
            ProductNotFound = 1,
            ProductNotAccepted = 2,
            NoEligibleOrders = 3,
            NoFreeBox = 4,
            SelectedBoxInUse = 5,
        }

    }
        public class DispatchProductResult
        {
            public DispatchProductResultType DispatchProductResultType { get; }
            public string ProductId { get; }
            public ObservableList<string> OrdersWithProduct { get; }

            public DispatchProductResult( DispatchProductResultType dispatchProductResultType, string productId = null, IEnumerable<string> ordersWithProduct = null )
            {
                DispatchProductResultType = dispatchProductResultType;
                ProductId = productId;
                if( ordersWithProduct != null )
                {
                    OrdersWithProduct = new ObservableList<string>();
                    OrdersWithProduct.AddRange( ordersWithProduct );
                }
            }

            public bool Success
                => DispatchProductResultType == DispatchProductResultType.Success;
        }

        public enum DispatchProductResultType
        {
            Success = 0,
            ProductNotFound = 1,
            ProductNotAccepted = 2,
            NoEligibleOrders = 3,
            NoFreeBox = 4,
            SelectedBoxInUse = 5,
        }

}
