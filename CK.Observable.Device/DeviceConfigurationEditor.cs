using CK.BinarySerialization;
using CK.Core;
using CK.Observable;
using CK.Observable.Device;
using CK.DeviceModel;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System;
using Microsoft.Extensions.Hosting;

namespace CK.Observable.Device
{
    [SerializationVersion( 0 )]
    public abstract class DeviceConfigurationEditor : InternalObject
    {
        readonly ObservableDeviceObject _owner;
        internal DeviceConfiguration _local;

        public DeviceConfiguration Local => _local;

        private protected DeviceConfigurationEditor( ObservableDeviceObject owner )
        {
            _owner = owner;
            if( _owner.Configuration != null )
            {
                _local = _owner.Configuration.DeepClone();
            }
            else
            {
                _owner.ConfigurationChanged += OnConfigChanged;
            }
        }

        private void OnConfigChanged( object sender )
        {
            var config = ((ObservableDeviceObject)sender).Configuration;
            if( config != null )
            {
                _local = config.DeepClone();
                _owner.ConfigurationChanged -= OnConfigChanged;
            }
        }

        protected DeviceConfigurationEditor( IBinaryDeserializer d, ITypeReadInfo info )
        : base( Sliced.Instance )
        {
            _owner = d.ReadObject<ObservableDeviceObject>();
            _local = d.ReadObject<DeviceConfiguration>();
        }

        public static void Write( IBinarySerializer s, in DeviceConfigurationEditor o )
        {
            s.WriteObject( o._owner );
            s.WriteObject( o._local );
        }

        public bool IsSame()
        {
            using( Domain.Monitor.OpenInfo( "Check Equality" ) )
            {
                if( !_local.CheckValid( Domain.Monitor ) )
                {
                    Throw.InvalidOperationException( "Local config is invalid" );
                }

                if( _owner.Configuration == null )
                {
                    Domain.Monitor.Warn( "Config owner is null" );
                    return false;
                }

                using( var localConfigStream = new MemoryStream() )
                using( var ownerConfigStream = new MemoryStream() )
                {
                    using( var serializer = BinarySerializer.Create( localConfigStream, new BinarySerializerContext() ) )
                    {
                        Domain.Monitor.Info( "Serialize Local Config" );
                        serializer.WriteObject( _local );
                    }

                    using( var serializer = BinarySerializer.Create( ownerConfigStream, new BinarySerializerContext() ) )
                    {
                        Domain.Monitor.Info( "Serialize Owner Config" );
                        serializer.WriteObject( _owner.Configuration );
                    }

                    if( localConfigStream.Length != ownerConfigStream.Length )
                    {
                        Domain.Monitor.Info( "Streams has not the same length, Equality is false" );
                        return false;
                    }

                    localConfigStream.Position = 0;
                    ownerConfigStream.Position = 0;

                    var localConfigStreamArray = localConfigStream.ToArray();
                    var ownerConfigStreamArray = ownerConfigStream.ToArray();

                    var equality = localConfigStreamArray.AsSpan()
                                                         .SequenceEqual( ownerConfigStreamArray.AsSpan() );

                    Domain.Monitor.Info( $"Equality of array is {equality}" );

                    return equality;
                }
            }
        }

        public void ApplyLocalConfig( DeviceControlAction? deviceControlAction )
        {
            if( !_local.CheckValid( Domain.Monitor ) ) Throw.InvalidOperationException( "Local config is invalid" );

            var shouldApply = false;

            var status = _owner.DeviceControlStatus;

            switch( deviceControlAction )
            {
                case DeviceControlAction.TakeControl:
                case DeviceControlAction.ReleaseControl:
                    if( status != DeviceControlStatus.OutOfControlByConfiguration )
                    {
                        shouldApply = true;
                    }
                    break;
                case DeviceControlAction.ForceReleaseControl:
                    if( _owner.Configuration!.ControllerKey != null )
                    {
                        _local.ControllerKey = null;
                        shouldApply = true;
                    }
                    break;
                case DeviceControlAction.SafeTakeControl:
                case DeviceControlAction.SafeReleaseControl:
                    if( status == DeviceControlStatus.HasControl || status == DeviceControlStatus.HasSharedControl )
                    {
                        shouldApply = true;
                    }
                    break;
                case DeviceControlAction.TakeOwnership:
                    if( _owner.Configuration!.ControllerKey != Domain.DomainName )
                    {
                        _local.ControllerKey = Domain.DomainName;
                        shouldApply = true;
                    }
                    break;
                case null:
                    shouldApply = status == DeviceControlStatus.HasControl ||
                                  status == DeviceControlStatus.HasSharedControl ||
                                  status == DeviceControlStatus.HasOwnership;
                    break;
            }

            if( shouldApply )
            {
                if( deviceControlAction != null ) _owner.SendDeviceControlCommand( deviceControlAction.Value );
                _owner.ApplyDeviceConfiguration( _local.DeepClone() );
            }
        }

    }

}
