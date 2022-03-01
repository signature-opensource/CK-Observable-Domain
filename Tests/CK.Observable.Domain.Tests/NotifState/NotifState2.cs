using CK.Core;
using CK.Observable;
using System.Diagnostics;

namespace Signature.Process.Dispatching
{
    [CK.BinarySerialization.SerializationVersion( 0 )]
    [DebuggerDisplay( "NotificationState" )]
    public class NotificationState2 : ObservableRootObject
    {
        public ObservableChannel<DispatchProductResult> ProductDispatchErrors { get; }
        public ObservableChannel<CKExceptionData> Exceptions { get; }

        public BarcodeScannerState BarcodeScanner { get; }

        public NotificationState2()
        {
            BarcodeScanner = new BarcodeScannerState();
            ProductDispatchErrors = new ObservableChannel<DispatchProductResult>();
            Exceptions = new ObservableChannel<CKExceptionData>();
        }

        protected NotificationState2( CK.BinarySerialization.IBinaryDeserializer d, CK.BinarySerialization.ITypeReadInfo info )
                : base( CK.BinarySerialization.Sliced.Instance )
        {
            BarcodeScanner = d.ReadObject<BarcodeScannerState>();
            ProductDispatchErrors = d.ReadObject<ObservableChannel<DispatchProductResult>>();
            Exceptions = d.ReadObject<ObservableChannel<CKExceptionData>>();
        }

        public static void Write( CK.BinarySerialization.IBinarySerializer s, in NotificationState2 o )
        {
            s.WriteObject( o.BarcodeScanner );
            s.WriteObject( o.ProductDispatchErrors );
            s.WriteObject( o.Exceptions );
        }

        public void SendDispatchResult( DispatchProductResult r )
        {
            if( !r.Success )
            {
                ProductDispatchErrors.Send( r );
            }
        }

        public void SendException( CKExceptionData exceptionData )
        {
            Exceptions.Send( exceptionData );
        }
    }
}
