using CK.Testing;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace CK.Observable.Domain.Tests;

static class WhiteBoxAccess
{
    static FieldInfo _freeListField = typeof( ObservableDomain ).GetField( "_freeList", BindingFlags.Instance | BindingFlags.NonPublic );

    public static List<int> GetFreeList( this ObservableDomain d )
    {
        return (List<int>)_freeListField.GetValue( d );
    }

}
