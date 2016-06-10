using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppCacheFactory
{
    interface ITileListComposer
    {
        List<string> GetRequestList();
        string ToQueryString();
        void FromQueryString(NameValueCollection queryString);
    }
}
