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

        public NotificationState()
        {
            _productDispatchErrors = new ObservableList<Notification<DispatchProductResult>>();
            _exceptions = new ObservableList<Notification<CKExceptionData>>();
            BarcodeScanner = new BarcodeScannerState();
        }

        protected NotificationState( CK.BinarySerialization.IBinaryDeserializer d, CK.BinarySerialization.ITypeReadInfo info )
                : base( CK.BinarySerialization.Sliced.Instance )
        {
            if( info.Version != 0 ) throw new NotSupportedException( $"Cannot deserialize {nameof( NotificationState )} version {info.Version}" );

            _productDispatchErrors = d.ReadObject<ObservableList<Notification<DispatchProductResult>>>();
            _exceptions = d.ReadObject<ObservableList<Notification<CKExceptionData>>>();
            BarcodeScanner = d.ReadObject<BarcodeScannerState>();
        }

        public static void Write( CK.BinarySerialization.IBinarySerializer s, in NotificationState o )
        {
            s.WriteObject( o._productDispatchErrors );
            s.WriteObject( o._exceptions );
            s.WriteObject( o.BarcodeScanner );
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

    [SerializationVersion( 0 )]
    public class Notification<T> : ObservableObject where T : class
    {
        public Notification( T body )
        {
            Body = body;
        }

        Notification( CK.BinarySerialization.IBinaryDeserializer r, CK.BinarySerialization.ITypeReadInfo info )
            : base( CK.BinarySerialization.Sliced.Instance )
        {
            Body = r.ReadObject<T>();
        }

        public static void Write( CK.BinarySerialization.IBinarySerializer s, in Notification<T> o )
        {
            s.WriteObject( o.Body );
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

        BarcodeScannerState( CK.BinarySerialization.IBinaryDeserializer r, CK.BinarySerialization.ITypeReadInfo info )
                : base( CK.BinarySerialization.Sliced.Instance )
        {
            LastSeen = r.Reader.ReadDateTime();
            LastScanIdentifier = r.Reader.ReadNullableString();
            LastScanMetadata = r.ReadObject<ObservableDictionary<string, string>>();
        }

        public static void Write( CK.BinarySerialization.IBinarySerializer s, in BarcodeScannerState o )
        {
            s.Writer.Write( o.LastSeen );
            s.Writer.WriteNullableString( o.LastScanIdentifier );
            s.WriteObject( o.LastScanMetadata );
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

    [SerializationVersion( 0)]
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

        public DispatchProductResult( CK.BinarySerialization.IBinaryDeserializer r, CK.BinarySerialization.ITypeReadInfo info )
        {
            DispatchProductResultType = r.ReadValue<DispatchProductResultType>();
            ProductId = r.Reader.ReadNullableString();
            OrdersWithProduct = r.ReadObject<ObservableList<string>>();
        }

        public static void Write( CK.BinarySerialization.IBinarySerializer w, in DispatchProductResult o )
        {
            w.WriteValue( o.DispatchProductResultType );
            w.Writer.WriteNullableString( o.ProductId );
            w.WriteObject( o.OrdersWithProduct );
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
