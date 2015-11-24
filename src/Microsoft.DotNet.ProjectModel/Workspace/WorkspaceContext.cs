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

        private WorkspaceContext(List<string> projectPaths, string configuration)
        {
            Configuration = configuration;
            Initialize(projectPaths);
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
            var projectPaths = PathResolve(projectPath);
            if (projectPaths == null || !projectPaths.Any())
            {
                return null;
            }

            var context = new WorkspaceContext(projectPaths, configuration);
            return context;
        }

        public static WorkspaceContext CreateFrom(string projectPath)
        {
            return CreateFrom(projectPath, "Debug");
        }

        private void Initialize(List<string> projectPaths)
        {
            _projects.Clear();
            foreach (var projectDirectory in projectPaths)
            {
                var project = GetProject(projectDirectory);

                if (project == null)
                {
                    continue;
                }

                _projects.Add(project.ProjectFilePath);

                foreach (var framework in project.GetTargetFrameworks())
                {
                    var projectReferences = GetProjectReferences(project, framework.FrameworkName);
                    foreach (var eachReference in projectReferences)
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

        public IEnumerable<DependencyDescription> GetDependencies(Project project, NuGetFramework framework)
        {
            return GetProjectContextWithDependencies(project, framework).Dependencies;
        }

        public IEnumerable<string> GetFileReferences(Project project, NuGetFramework framework)
        {
            return GetProjectContextWithDependencies(project, framework).FileReferences;
        }

        public IEnumerable<string> GetSourceFiles(Project project, NuGetFramework framework)
        {
            return GetProjectContextWithDependencies(project, framework).SourceFiles;
        }

        public IEnumerable<ProjectReferenceInfo> GetProjectReferences(Project project, NuGetFramework framework)
        {
            return GetProjectContextWithDependencies(project, framework).ProjectReferences;
        }

        public IEnumerable<DiagnosticMessage> GetDiagnostics(Project project, NuGetFramework framework)
        {
            return GetProjectContextWithDependencies(project, framework).Diagnostics;
        }

        private ProjectContextEntry GetProjectContextWithDependencies(Project project, NuGetFramework framework)
        {
            return _projectContextsCache.AddOrUpdate(
                Tuple.Create(project.ProjectDirectory, framework),
                key => AddProjectContextEntryWithDependency(key.Item1, key.Item2, null),
                (key, oldEntry) => AddProjectContextEntryWithDependency(key.Item1, key.Item2, oldEntry));
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
            if (!entry.HasDependencyResolved)
            {
                entry.ResolveDependencies(Configuration);
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
