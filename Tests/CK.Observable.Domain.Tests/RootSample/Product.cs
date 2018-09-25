using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable.Domain.Tests.RootSample
{
    [Serializable]
    public class Product
    {
        public Product( string n, int p )
        {
            Name = n;
            Power = p;
            ExtraData = new Dictionary<string, string>();
        }

        public string Name { get; }

        public int Power { get; }

        public IDictionary<string,string> ExtraData { get; }
    }
}
