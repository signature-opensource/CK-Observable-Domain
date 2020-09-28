using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable.Device
{
    public enum DeviceControlRequestStatus
    {
        /// <summary>
        /// The control has not been requested.
        /// </summary>
        None,

        /// <summary>
        /// The control is currently being requested.
        /// </summary>
        RequestingControl,

        /// <summary>
        /// The control has been requested but failed to be obtained.
        /// </summary>
        RequestedFailed,

        /// <summary>
        /// The control has been requested and has been obtained.
        /// </summary>
        RequestedSucceeded,
    }
}
