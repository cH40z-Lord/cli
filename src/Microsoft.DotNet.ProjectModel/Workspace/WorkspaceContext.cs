// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.ProjectModel;
using Microsoft.Extensions.ProjectModel.Graph;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectModel.Workspace
{
    public class WorkspaceContext
    {
        // key: project directory
        private readonly ConcurrentDictionary<string, FileModelEntry<Project>> _projectsCache
                   = new ConcurrentDictionary<string, FileModelEntry<Project>>();

        // key: project directory
        private readonly ConcurrentDictionary<string, FileModelEntry<LockFile>> _lockFileCache
                   = new ConcurrentDictionary<string, FileModelEntry<LockFile>>();

        // key: project directory, target framework
        private readonly ConcurrentDictionary<Tuple<string, NuGetFramework>, ProjectContextEntry> _projectContextsCache
                   = new ConcurrentDictionary<Tuple<string, NuGetFramework>, ProjectContextEntry>();

        private readonly HashSet<string> _projects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private WorkspaceContext(List<string> projectCandidates, string configuration)
        {
            Configuration = configuration;
            Initialize(projectCandidates);
        }

        public string Configuration { get; }

        /// <summary>
        /// Create a WorkspaceContext from a given path.
        /// 
        /// There must be either a global.json or project.json at under the given path. Otherwise
        /// null is returned.
        /// </summary>
        public static WorkspaceContext CreateFrom(string projectPath, string configuration)
        {
            var projectCandiates = PathResolve(projectPath);
            if (projectCandiates == null || !projectCandiates.Any())
            {
                return null;
            }

            var context = new WorkspaceContext(projectCandiates, configuration);
            return context;
        }

        public static WorkspaceContext CreateFrom(string projectPath)
        {
            return CreateFrom(projectPath, "Debug");
        }

        private void Initialize(List<string> projectCandidates)
        {
            _projects.Clear();
            foreach (var projectDirectory in projectCandidates)
            {
                var project = GetProject(projectDirectory);

                if (project == null)
                {
                    continue;
                }

                _projects.Add(project.ProjectFilePath);

                foreach (var framework in project.GetTargetFrameworks())
                {
                    var depInfo = GetDependencyInfo(project, framework.FrameworkName);
                    foreach (var eachReference in depInfo.ProjectReferences)
                    {
                        var referencedProject = GetProject(eachReference.Path);
                        if (referencedProject == null)
                        {
                            continue;
                        }

                        _projects.Add(referencedProject.ProjectFilePath);
                    }
                }
            }
        }

        public IEnumerable<string> Projects => _projects;

        public Project GetProject(string projectDirectory)
        {
            return _projectsCache.AddOrUpdate(
                projectDirectory,
                key => AddProjectEntry(key, null),
                (key, oldEntry) => AddProjectEntry(key, oldEntry)).Model;
        }

        public LockFile GetLockFile(string projectDirectory)
        {
            return _lockFileCache.AddOrUpdate(
                projectDirectory,
                key => AddLockFileEntry(key, null),
                (key, oldEntry) => AddLockFileEntry(key, oldEntry)).Model;
        }

        public ProjectContext GetProjectContext(Project project, NuGetFramework framework)
        {
            return _projectContextsCache.AddOrUpdate(
                Tuple.Create(project.ProjectDirectory, framework),
                key => AddProjectContextEntry(key.Item1, key.Item2, null),
                (key, oldEntry) => AddProjectContextEntry(key.Item1, key.Item2, oldEntry)).ProjectContext;
        }

        public DependencyInfo GetDependencyInfo(Project project, NuGetFramework framework)
        {
            return _projectContextsCache.AddOrUpdate(
                Tuple.Create(project.ProjectDirectory, framework),
                key => AddProjectContextEntryWithDependency(key.Item1, key.Item2, null),
                (key, oldEntry) => AddProjectContextEntryWithDependency(key.Item1, key.Item2, oldEntry)).DependencyInfo;
        }

        private FileModelEntry<LockFile> AddLockFileEntry(string projectDirectory, FileModelEntry<LockFile> currentEntry)
        {
            if (currentEntry == null)
            {
                currentEntry = new FileModelEntry<LockFile>();
            }
            else if (!File.Exists(Path.Combine(projectDirectory, LockFile.FileName)))
            {
                currentEntry.Reset();
                return currentEntry;
            }

            if (currentEntry.IsInvalid)
            {
                currentEntry.FilePath = Path.Combine(projectDirectory, LockFile.FileName);
                currentEntry.Model = LockFileReader.Read(currentEntry.FilePath);
                currentEntry.UpdateLastWriteTime();
            }

            return currentEntry;
        }

        private FileModelEntry<Project> AddProjectEntry(string projectDirectory, FileModelEntry<Project> currentEntry)
        {
            if (currentEntry == null)
            {
                currentEntry = new FileModelEntry<Project>();
            }
            else if (!File.Exists(Path.Combine(projectDirectory, Project.FileName)))
            {
                // project was deleted
                currentEntry.Reset();
                return currentEntry;
            }

            if (currentEntry.IsInvalid)
            {
                Project project;
                if (!ProjectReader.TryGetProject(projectDirectory, out project))
                {
                    currentEntry.Reset();
                }
                else
                {
                    currentEntry.Model = project;
                    currentEntry.FilePath = project.ProjectFilePath;
                    currentEntry.UpdateLastWriteTime();
                }
            }

            return currentEntry;
        }

        private ProjectContextEntry AddProjectContextEntry(string projectDirectory,
                                                           NuGetFramework framework,
                                                           ProjectContextEntry currentEntry)
        {
            if (currentEntry == null)
            {
                // new entry required
                currentEntry = new ProjectContextEntry();
            }

            var project = GetProject(projectDirectory);
            if (project == null)
            {
                // project doesn't exist anymore
                currentEntry.Reset();
                return currentEntry;
            }

            if (currentEntry.HasChanged)
            {
                var builder = new ProjectContextBuilder()
                    .WithProjectResolver(path => GetProject(path))
                    .WithLockFileResolver(path => GetLockFile(path))
                    .WithProject(project)
                    .WithTargetFramework(framework);

                currentEntry.ProjectContext = builder.Build();
            }

            return currentEntry;
        }

        private ProjectContextEntry AddProjectContextEntryWithDependency(string projectDirectory,
                                                                         NuGetFramework framework,
                                                                         ProjectContextEntry currentEntry)
        {
            var entry = AddProjectContextEntry(projectDirectory, framework, currentEntry);

            if (entry.ProjectContext != null && entry.DependencyInfo == null)
            {
                entry.DependencyInfo = entry.ProjectContext.GetDependencyInfo(Configuration);
            }

            return entry;
        }

        private class FileModelEntry<TModel> where TModel : class
        {
            private DateTime _lastWriteTime;

            public TModel Model { get; set; }

            public string FilePath { get; set; }

            public void UpdateLastWriteTime()
            {
                _lastWriteTime = File.GetLastWriteTime(FilePath);
            }

            public bool IsInvalid
            {
                get
                {
                    if (Model == null)
                    {
                        return true;
                    }

                    if (!File.Exists(FilePath))
                    {
                        return true;
                    }

                    return _lastWriteTime < File.GetLastWriteTime(FilePath);
                }
            }

            public void Reset()
            {
                Model = null;
                FilePath = null;
                _lastWriteTime = DateTime.MinValue;
            }
        }

        private class ProjectContextEntry
        {
            private ProjectContext _context;

            public ProjectContext ProjectContext
            {
                get { return _context; }
                set
                {
                    if (value == null)
                    {
                        LastLockFileWriteTime = DateTime.MinValue;
                    }
                    else
                    {
                        var lockFilePath = Path.Combine(value.ProjectFile.ProjectDirectory, LockFile.FileName);
                        LastLockFileWriteTime = File.Exists(lockFilePath) ? File.GetLastWriteTime(lockFilePath) :
                                                                            DateTime.MinValue;
                    }

                    DependencyInfo = null;
                    _context = value;
                }
            }

            public DependencyInfo DependencyInfo { get; set; }

            public DateTime LastLockFileWriteTime { get; private set; }

            public bool HasChanged
            {
                get
                {
                    if (ProjectContext == null)
                    {
                        return true;
                    }

                    var lockFilePath = Path.Combine(ProjectContext.ProjectFile.ProjectDirectory,
                                                    LockFile.FileName);

                    return LastLockFileWriteTime < File.GetLastWriteTime(lockFilePath);
                }
            }

            public void Reset()
            {
                _context = null;
                DependencyInfo = null;
                LastLockFileWriteTime = DateTime.MinValue;
            }
        }

        private static List<string> PathResolve(string projectPath)
        {
            if (File.Exists(projectPath))
            {
                var filename = Path.GetFileName(projectPath);
                if (!Project.FileName.Equals(filename, StringComparison.OrdinalIgnoreCase) &&
                    !GlobalSettings.FileName.Equals(filename, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                projectPath = Path.GetDirectoryName(projectPath);
            }

            if (File.Exists(Path.Combine(projectPath, Project.FileName)))
            {
                return new List<string> { projectPath };
            }

            if (File.Exists(Path.Combine(projectPath, GlobalSettings.FileName)))
            {
                var root = ProjectRootResolver.ResolveRootDirectory(projectPath);
                GlobalSettings globalSettings;
                if (GlobalSettings.TryGetGlobalSettings(projectPath, out globalSettings))
                {
                    return globalSettings.ProjectSearchPaths
                                         .Select(searchPath => Path.Combine(globalSettings.DirectoryPath, searchPath))
                                         .Where(actualPath => Directory.Exists(actualPath))
                                         .SelectMany(actualPath => Directory.GetDirectories(actualPath))
                                         .Where(actualPath => File.Exists(Path.Combine(actualPath, Project.FileName)))
                                         .Select(path => Path.GetFullPath(path))
                                         .Distinct(StringComparer.OrdinalIgnoreCase)
                                         .ToList();
                }
            }

            return null;
        }
    }
}
