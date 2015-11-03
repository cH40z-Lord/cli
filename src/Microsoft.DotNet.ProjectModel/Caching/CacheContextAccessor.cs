using System;

namespace Microsoft.DotNet.ProjectModel.Caching
{
    internal class CacheContextAccessor : ICacheContextAccessor
    {
        [ThreadStatic]
        private static CacheContext _threadInstance;

        public static CacheContext ThreadInstance
        {
            get { return _threadInstance; }
            set { _threadInstance = value; }
        }

        public CacheContext Current
        {
            get { return ThreadInstance; }
            set { ThreadInstance = value; }
        }
    }
}