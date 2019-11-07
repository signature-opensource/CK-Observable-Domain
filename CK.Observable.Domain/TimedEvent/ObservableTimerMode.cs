using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    /// <summary>
    /// Defines the behavior of the <see cref="ObservableTimer"/> regarding the adjustment of its <see cref="ObservableTimer.DueTimeUtc"/>.
    /// </summary>
    public enum ObservableTimerMode
    {
        /// <summary>
        /// Adjustment of actual due time is allowed by skipping any number of events.
        /// (A <see cref="Core.LogLevel.Warn"/> is nevertheless logged if this happens.)
        /// </summary>
        Relaxed = 0,

        /// <summary>
        /// Adjustment of actual due time is allowed as long as no event is lost.
        /// A <see cref="Core.LogLevel.Warn"/> is always logged if this happens.
        /// If an event is lost, an error is logged or an exception is thrown depdending on <see cref="ThrowException"/> bit.
        /// </summary>
        AllowSlidingAdjustment = 1,

        /// <summary>
        /// Adjustment of actual due time is allowed by skipping at most one event.
        /// A <see cref="Core.LogLevel.Warn"/> is always logged if this happens.
        /// If more than one event is lost, an error is logged or an exception is thrown depdending on <see cref="ThrowException"/> bit.
        /// </summary>
        AllowOneStepAdjustment = 2,

        /// <summary>
        /// Any due time adjustment is forbidden. No event must be lost.
        /// Any time adjustment logs an error or throws an exception depdending on <see cref="ThrowException"/> bit.
        /// </summary>
        Critical = 3,

        /// <summary>
        /// When this bit is set, any violation of <see cref="AllowSlidingAdjustment"/>, <see cref="AllowOneStepAdjustment"/> or <see cref="Critical"/>
        /// mode raises an exception instead of logging an error.
        /// </summary>
        ThrowException = 4
    }


}
