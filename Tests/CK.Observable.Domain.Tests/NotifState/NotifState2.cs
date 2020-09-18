using CK.Core;
using CK.Observable;
using System.Diagnostics;

namespace Signature.Process.Dispatching
{
    [SerializationVersion( 0 )]
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

        protected NotificationState2( IBinaryDeserializerContext d ) : base( d )
        {
            var r = d.StartReading();
            BarcodeScanner = (BarcodeScannerState)r.ReadObject();
            ProductDispatchErrors = (ObservableChannel<DispatchProductResult>)r.ReadObject();
            Exceptions = (ObservableChannel<CKExceptionData>)r.ReadObject();
        }

        void Write( BinarySerializer w )
        {
            w.WriteObject( BarcodeScanner );
            w.WriteObject( ProductDispatchErrors );
            w.WriteObject( Exceptions );
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
