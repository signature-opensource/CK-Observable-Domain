using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable
{
    /// <summary>
    /// Extends <see cref="IDestroyable"/> to expose the <see cref="IDestroyableObject.Destroy()"/> method.
    /// </summary>
    public interface IDestroyableObject : IDestroyable
    {
        /// <summary>
        /// Destroys this object.
        /// </summary>
        void Destroy();
    }
}
