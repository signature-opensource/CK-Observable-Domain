using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable.League
{
    /// <summary>
    /// Options for <see cref="DefaultObservableLeagueOptions"/>.
    /// </summary>
    public class DefaultObservableLeagueOptions
    {
        /// <summary>
        /// Gets or sets the <see cref="DefaultObservableLeague"/> path of its store.
        /// When relative, it is based on the <see cref="AppContext.BaseDirectory"/>.
        /// </summary>
        public string? StorePath { get; set; }
    }
}
