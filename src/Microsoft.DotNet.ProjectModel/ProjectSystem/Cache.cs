// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.ProjectModel.ProjectSystem
{
    internal class Cache
    {
        [ThreadStatic]
        private static CacheContext _threadCacheContextInstance;

        private readonly ConcurrentDictionary<string, NamedCacheDependency> _namedDependencies
                   = new ConcurrentDictionary<string, NamedCacheDependency>(StringComparer.OrdinalIgnoreCase);

        private readonly ConcurrentDictionary<object, Lazy<CacheEntry>> _entries
                   = new ConcurrentDictionary<object, Lazy<CacheEntry>>();

        public object Get(object key, Func<CacheContext, object> factory)
        {
            var entry = _entries.AddOrUpdate(key,
                k => AddEntry(k, factory),
                (k, oldValue) => UpdateEntry(oldValue, k, factory));

            return entry.Value.Result;
        }

        public void TriggerDependency(params string[] clues)
        {
            NamedCacheDependency dependency;
            if (_namedDependencies.TryRemove(CreateDependencyName(clues), out dependency))
            {
                dependency.SetChanged();
            }
        }

        public void MonitorFile(CacheContext ctx, string filepath)
        {
            ctx.Monitor(new FileWriteTimeCacheDependency(filepath));
        }

        public void MonitorDependency(CacheContext ctx, params string[] clues)
        {
            var name = CreateDependencyName(clues);
            var dependency = _namedDependencies.GetOrAdd(name, key => new NamedCacheDependency(key));
            ctx.Monitor(dependency);
        }

        private string CreateDependencyName(params string[] clues) => $"DependencyName_of_{string.Join("_", clues)}";

        private Lazy<CacheEntry> AddEntry(object k, Func<CacheContext, object> acquire)
        {
            return new Lazy<CacheEntry>(() =>
            {
                var entry = CreateEntry(k, acquire);
                PropagateCacheDependencies(entry);
                return entry;
            });
        }

        private Lazy<CacheEntry> UpdateEntry(Lazy<CacheEntry> currentEntry, object k, Func<CacheContext, object> acquire)
        {
            try
            {
                bool expired = currentEntry.Value.Dependencies.Any(t => t.HasChanged);

                if (expired)
                {
                    // Dispose any entries that are disposable since
                    // we're creating a new one
                    currentEntry.Value.Dispose();

                    return AddEntry(k, acquire);
                }
                else
                {
                    // Logger.TraceInformation("[{0}]: Cache hit for {1}", GetType().Name, k);

                    // Already evaluated
                    PropagateCacheDependencies(currentEntry.Value);
                    return currentEntry;
                }
            }
            catch (Exception)
            {
                return AddEntry(k, acquire);
            }
        }

        private void PropagateCacheDependencies(CacheEntry entry)
        {
            // Bubble up volatile tokens to parent context
            if (_threadCacheContextInstance != null)
            {
                foreach (var dependency in entry.Dependencies)
                {
                    _threadCacheContextInstance.Monitor(dependency);
                }
            }
        }

        private CacheEntry CreateEntry(object k, Func<CacheContext, object> acquire)
        {
            var entry = new CacheEntry();
            var context = new CacheContext(k, entry.AddCacheDependency);
            CacheContext parentContext = null;
            try
            {
                // Push context
                parentContext = _threadCacheContextInstance;
                _threadCacheContextInstance = context;

                entry.Result = acquire(context);
            }
            finally
            {
                // Pop context
                _threadCacheContextInstance = parentContext;
            }

            entry.CompactCacheDependencies();
            return entry;
        }

        private class CacheEntry : IDisposable
        {
            private IList<ICacheDependency> _dependencies;

            public CacheEntry() { }

            public IEnumerable<ICacheDependency> Dependencies => _dependencies ?? Enumerable.Empty<ICacheDependency>();

            public object Result { get; set; }

            public void AddCacheDependency(ICacheDependency cacheDependency)
            {
                if (_dependencies == null)
                {
                    _dependencies = new List<ICacheDependency>();
                }

                _dependencies.Add(cacheDependency);
            }

            public void CompactCacheDependencies()
            {
                if (_dependencies != null)
                {
                    _dependencies = _dependencies.Distinct().ToArray();
                }
            }

            public void Dispose()
            {
                (Result as IDisposable)?.Dispose();
            }
        }

        private class NamedCacheDependency : ICacheDependency
        {
            private readonly string _name;
            private bool _hasChanged;

            public NamedCacheDependency(string name)
            {
                _name = name;
            }

            public void SetChanged()
            {
                _hasChanged = true;
            }

            public bool HasChanged => _hasChanged;
        }

        private class FileWriteTimeCacheDependency : ICacheDependency
        {
            private readonly string _path;
            private readonly DateTime _lastWriteTime;

            public FileWriteTimeCacheDependency(string path)
            {
                _path = path;
                _lastWriteTime = File.GetLastWriteTime(path);
            }

            public bool HasChanged => _lastWriteTime < File.GetLastWriteTime(_path);

            public override string ToString() => _path;

            public override bool Equals(object obj)
            {
                var token = obj as FileWriteTimeCacheDependency;
                return token != null && token._path.Equals(_path, StringComparison.OrdinalIgnoreCase);
            }

            public override int GetHashCode()
            {
                return _path.GetHashCode();
            }
        }
    }
}