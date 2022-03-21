using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public partial class ObservableDomain
    {
        /// <summary>
        /// Immutable capture of object graph issues.
        /// This is (efficiently) computed by the <see cref="Save"/> method. Note that because of concurrent executions,
        /// unreachable objects appearing in these lists may already be destroyed when this object is exposed.
        /// </summary>
        public sealed class LostObjectTracker
        {
            internal LostObjectTracker( ObservableDomain d,
                                        IReadOnlyList<ObservableObject>? observables,
                                        IReadOnlyList<InternalObject>? internals,
                                        IReadOnlyList<ObservableTimedEventBase>? timed,
                                        IReadOnlyList<BinarySerialization.IDestroyable>? refDestroyed,
                                        int unusedPooledReminders,
                                        int pooledReminderCount )
            {
                TransactionNumber = d.TransactionSerialNumber;
                UnreacheableObservables = observables ?? Array.Empty<ObservableObject>();
                UnreacheableInternals = internals ?? Array.Empty<InternalObject>();
                UnreacheableTimedObjects = timed ?? Array.Empty<ObservableTimedEventBase>();
                ReferencedDestroyed = refDestroyed ?? Array.Empty<BinarySerialization.IDestroyable>();
                UnusedPooledReminderCount = unusedPooledReminders;
                PooledReminderCount = pooledReminderCount;
            }

            /// <summary>
            /// Gets the transaction number of the domain that has been captured.
            /// </summary>
            public int TransactionNumber { get; }

            /// <summary>
            /// Gets a list of <see cref="IDestroyable"/> that are destroyed but are
            /// still referenced from non destroyed objects.
            /// </summary>
            public IReadOnlyList<BinarySerialization.IDestroyable> ReferencedDestroyed { get; }

            /// <summary>
            /// Gets a list of non destroyed <see cref="ObservableObject"/> that are no more reachable
            /// from any of <see cref="ObservableDomain.AllRoots"/>.
            /// <para>
            /// Note that when there is no defined root, this list is empty and that, because of concurrent executions,
            /// some of these object may be already destroyed.
            /// </para>
            /// </summary>
            public IReadOnlyList<ObservableObject> UnreacheableObservables { get; }

            /// <summary>
            /// Gets a list of non destroyed <see cref="InternalObject"/> that are no more reachable
            /// from any of <see cref="ObservableDomain.AllRoots"/>.
            /// <para>
            /// Note that when there is no defined root, this list is empty and that, because of concurrent executions,
            /// some of these object may be already destroyed.
            /// </para>
            /// </summary>
            public IReadOnlyList<InternalObject> UnreacheableInternals { get; }

            /// <summary>
            /// Gets a list of non destroyed <see cref="ObservableTimedEventBase"/> that are no more reachable
            /// from any of <see cref="ObservableDomain.AllRoots"/> (and are not pooled reminders).
            /// <para>
            /// Note that when there is no defined root, this list is empty and that, because of concurrent executions,
            /// some of these object may be already destroyed.
            /// </para>
            /// </summary>
            public IReadOnlyList<ObservableTimedEventBase> UnreacheableTimedObjects { get; }

            /// <summary>
            /// Gets the number of unused pooled reminders.
            /// </summary>
            public int UnusedPooledReminderCount { get; }

            /// <summary>
            /// Gets the total number of pooled reminders.
            /// </summary>
            public int PooledReminderCount { get; }

            /// <summary>
            /// When <see cref="UnusedPooledReminderCount"/> is greater than 16 and greater than half of the <see cref="PooledReminderCount"/>,
            /// the <see cref="ObservableDomain.GarbageCollectAsync"/> will trim the number of pooled reminders.
            /// </summary>
            public bool ShouldTrimPooledReminders => UnusedPooledReminderCount > 16 && (2 * UnusedPooledReminderCount) > PooledReminderCount;

            /// <summary>
            /// Gets whether one or more issues have been detected.
            /// When false, then there is nothing to do (it is useless to call <see cref="ObservableDomain.GarbageCollectAsync(IActivityMonitor, int)"/>).
            /// </summary>
            public bool HasIssues => ReferencedDestroyed.Count > 0
                                     || UnreacheableObservables.Count > 0
                                     || UnreacheableInternals.Count > 0
                                     || UnreacheableTimedObjects.Count > 0
                                     || ShouldTrimPooledReminders;

            /// <summary>
            /// Dumps the messages to the monitor. Only the <see cref="ReferencedDestroyed"/> are errors.
            /// Other issues are expressed as warnings.
            /// </summary>
            /// <param name="monitor">The target monitor.</param>
            /// <param name="dumpReferencedDestroyed">False to skip <see cref="ReferencedDestroyed"/> errors.</param>
            public void DumpLog( IActivityMonitor monitor, bool dumpReferencedDestroyed = true )
            {
                if( dumpReferencedDestroyed )
                {
                    if( ReferencedDestroyed.Count > 0 )
                    {
                        using( monitor.OpenError( $"{ReferencedDestroyed.Count} destroyed objects are referenced by one or more non destroyed objects." ) )
                        {
                            monitor.Error( ReferencedDestroyed.GroupBy( r => r.GetType() ).Select( g => $"{g.Count()} of type '{g.Key.Name}'" ).Concatenate() );
                        }
                    }
                    else
                    {
                        monitor.Trace( "No reference to destroyed objects." );
                    }
                }
                if( UnreacheableObservables.Count > 0 )
                {
                    using( monitor.OpenWarn( $"{UnreacheableObservables.Count} Observable objects are not reachable from any of the domain's roots." ) )
                    {
                        monitor.Warn( UnreacheableObservables.GroupBy( r => r.GetType() ).Select( g => $"{g.Count()} of type '{g.Key.Name}'" ).Concatenate() );
                    }
                }
                else
                {
                    monitor.Trace( "No unreachable Observable objects found." );
                }
                if( UnreacheableInternals.Count > 0 )
                {
                    using( monitor.OpenWarn( $"{UnreacheableInternals.Count} Internal objects are not reachable from any of the domain's roots." ) )
                    {
                        monitor.Warn( UnreacheableInternals.GroupBy( r => r.GetType() ).Select( g => $"{g.Count()} of type '{g.Key.Name}'" ).Concatenate() );
                    }
                }
                else
                {
                    monitor.Trace( "No unreachable Internal objects found." );
                }
                if( UnreacheableTimedObjects.Count > 0 )
                {
                    using( monitor.OpenWarn( $"{UnreacheableTimedObjects.Count} Timer or Reminder objects are not reachable from any of the domain's roots." ) )
                    {
                        monitor.Warn( UnreacheableTimedObjects.GroupBy( r => r.GetType() ).Select( g => $"{g.Count()} of type '{g.Key.Name}'" ).Concatenate() );
                    }
                }
                else
                {
                    monitor.Trace( "No unreachable Timer or Reminder objects found." );
                }
                if( ShouldTrimPooledReminders )
                {
                    monitor.Warn( $"There are {UnusedPooledReminderCount} unused pooled reminders out of {PooledReminderCount}. The set of pooled reminders should be trimmed." );
                }
            }

            /// <summary>
            /// Overridden to return the count of the different lists.
            /// </summary>
            /// <returns>A readable string.</returns>
            public override string ToString() => $"{ReferencedDestroyed.Count} ReferencedDestroyed, {UnreacheableObservables.Count} UnreacheableObservables, {UnreacheableInternals.Count} UnreacheableInternals, {UnreacheableTimedObjects.Count} UnreacheableTimedObjects.";
        }

        public LostObjectTracker? CurrentLostObjectTracker { get; private set; }


        class NullStream : Stream
        {
            long _p;

            public override bool CanRead => false;

            public override bool CanSeek => false;

            public override bool CanWrite => true;

            public override long Length => throw new NotSupportedException();

            public override long Position { get => _p; set => throw new NotSupportedException(); }

            public override void Flush() { }

            public override int Read( byte[] buffer, int offset, int count )
            {
                throw new NotSupportedException();
            }

            public override long Seek( long offset, SeekOrigin origin )
            {
                throw new NotSupportedException();
            }

            public override void SetLength( long value )
            {
                throw new NotSupportedException();
            }

            public override void Write( byte[] buffer, int offset, int count )
            {
                _p += count;
            }
        }

        /// <inheritdoc />
        public async Task<bool> GarbageCollectAsync( IActivityMonitor monitor, int millisecondsTimeout = -1 )
        {
            CheckDisposed();
            using( monitor.OpenInfo( $"Garbage collecting." ) )
            {
                var c = EnsureLostObjectTracker( monitor, millisecondsTimeout );
                if( c == null ) return false;
                if( !c.HasIssues )
                {
                    monitor.CloseGroup( "There is nothing to do." );
                    return true;
                }
                c.DumpLog( monitor, false );
                int count = 0;
                var (ex, result) = await ModifyNoThrowAsync( monitor, () =>
                {
                    Debug.Assert( c != null );

                    // Destroyed objects can only transition from alive to destroyed: using
                    // the lost objects captured here is fine since the only risk is to forget
                    // some objects.
                    foreach( var o in c.UnreacheableObservables )
                    {
                        if( !o.IsDestroyed )
                        {
                            o.Unload( gc: true );
                            ++count;
                        }
                    }
                    foreach( var o in c.UnreacheableInternals )
                    {
                        if( !o.IsDestroyed )
                        {
                            o.Unload( gc: true );
                            ++count;
                        }
                    }
                    foreach( var o in c.UnreacheableTimedObjects )
                    {
                        if( !o.IsDestroyed )
                        {
                            o.Destroy();
                            ++count;
                        }
                    }
                    //// Pool reminders may have been added/removed to the pool by transactions
                    //// before we enter this ModifyAsync.
                    //// We should theoretically reanalyze the data but since we ask to
                    //// remove only half of the unused (at most), we do it directly.
                    if( c.ShouldTrimPooledReminders )
                    {
                        TimeManager.TrimPooledReminders( monitor, c.UnusedPooledReminderCount / 2 );
                    }
                }, millisecondsTimeout );
                if( ex != null )
                {
                    monitor.Error( ex );
                    return false;
                }
                monitor.CloseGroup( $"Removed {count} objects." );
                return result.Success;
            }
        }

        /// <inheritdoc />
        public LostObjectTracker? EnsureLostObjectTracker( IActivityMonitor monitor, int millisecondsTimeout = -1 )
        {
            // We don't need synchronization code here: the "CurrentLostObjectTracker" may have been
            // updated by another save and we absolutely don't care since the LostObjectTracker creation is
            // not parametrized: it's the same for everyone.
            var current = CurrentLostObjectTracker;
            if( current == null || current.TransactionNumber != TransactionSerialNumber )
            {
                using var s = new NullStream();
                monitor.Trace( "Saving objects in a null stream to track lost objects." );
                if( !Save( monitor, s, millisecondsTimeout: millisecondsTimeout ) )
                {
                    return null;
                }
            }
            return CurrentLostObjectTracker;
        }

    }
}
