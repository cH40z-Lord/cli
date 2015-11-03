using System;

namespace Microsoft.DotNet.ProjectModel.Caching
{
    internal interface ICache
    {
        object Get(object key, Func<CacheContext, object> factory);

        object Get(object key, Func<CacheContext, object, object> factory);
    }
}
