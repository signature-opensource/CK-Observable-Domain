using System;

namespace CK.Observable
{
    /// <summary>
    /// When applied to a class, a struct or a property, specifies that it must not be exported and hence
    /// must not participate in remote domain synchronization.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property|AttributeTargets.Class|AttributeTargets.Struct, AllowMultiple = false, Inherited = true)]
    public class NotExportableAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets an optional error message that will appear in a <see cref="CK.Core.CKException"/>'s <see cref="Exception.Message"/>
        /// if the type or property is encountered.
        /// </summary>
        public string? Error { get; set; }
    }
}
