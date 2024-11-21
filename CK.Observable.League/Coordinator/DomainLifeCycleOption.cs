using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable.League;

/// <summary>
/// Qualifies when a <see cref="ObservableDomain"/> managed by a <see cref="ObservableLeague"/> must
/// be loaded and kept in memory.
/// A domain must be loaded for its <see cref="ObservableTimer"/> and <see cref="ObservableReminder"/>
/// to be running.
/// </summary>
public enum DomainLifeCycleOption
{
    /// <summary>
    /// The domain is always loaded even if no code is currently using it and no active <see cref="ObservableTimedEventBase"/> exist.
    /// </summary>
    Always,

    /// <summary>
    /// The domain is unloaded unless explicitly loaded with one of
    /// the <see cref="IObservableDomainLoader.LoadAsync"/> method,
    /// regardless of any active <see cref="ObservableTimedEventBase"/>.
    /// </summary>
    Never,

    /// <summary>
    /// The domain is kept in memory as long as at least one active timer or reminder exist.
    /// It guaranties that domains are "alive" regardless of any actual code that use them.
    /// </summary>
    HonorTimedEvent
}
