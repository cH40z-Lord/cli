using System;

namespace Microsoft.DotNet.ProjectModel.Caching
{
    internal class CacheContext
    {
        public CacheContext(object key, Action<ICacheDependency> monitor)
        {
            Key = key;
            Monitor = monitor;
        }

        public object Key { get; }

        public Action<ICacheDependency> Monitor { get; }
    }
}