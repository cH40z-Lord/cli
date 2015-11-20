// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.ProjectModel.ProjectSystem
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