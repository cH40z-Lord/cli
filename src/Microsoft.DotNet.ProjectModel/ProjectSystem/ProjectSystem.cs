// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.ProjectModel;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectModel.ProjectSystem
{
    public class ProjectSystem
    {
        private readonly HashSet<string> _projectPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly ProjectSystemCache _cache = new ProjectSystemCache();

        public ProjectSystem(string projectPath)
        {
            var root = ProjectRootResolver.ResolveRootDirectory(projectPath);
            GlobalSettings globalSettings;
            if (GlobalSettings.TryGetGlobalSettings(projectPath, out globalSettings))
            {
                foreach (var searchPath in globalSettings.ProjectSearchPaths)
                {
                    var actualPath = Path.Combine(globalSettings.DirectoryPath, searchPath);
                    if (!Directory.Exists(actualPath))
                    {
                        continue;
                    }
                    else
                    {
                        actualPath = Path.GetFullPath(actualPath);
                    }

                    foreach (var dir in Directory.GetDirectories(actualPath))
                    {
                        var projectJson = Path.Combine(dir, Project.FileName);
                        if (File.Exists(projectJson))
                        {
                            _projectPaths.Add(projectJson);
                        }
                    }
                }
            }

            if (File.Exists(projectPath) &&
                string.Equals(Path.GetFileName(projectPath), Project.FileName, StringComparison.OrdinalIgnoreCase))
            {
                _projectPaths.Add(projectPath);
            }
        }

        public ISet<string> GetProjectPaths()
        {
            return _projectPaths;
        }

        public ProjectInformation GetProjectInformation(string projectPath)
        {
            var project = _cache.GetProject(projectPath);
            if (project == null)
            {
                return null;
            }
            else
            {
                return new ProjectInformation(project);
            }
        }

        public IEnumerable<ProjectReferenceInfo> GetProjectReferences(
            string projectPath,
            NuGetFramework framework,
            string configuration)
        {
            var dependencyInfo = _cache.GetDependencyInfo(projectPath, framework, configuration);
            if (dependencyInfo == null)
            {
                return Enumerable.Empty<ProjectReferenceInfo>();
            }

            return dependencyInfo.ProjectReferences;
        }

        public IEnumerable<DependencyDescription> GetDependencies(
            string projectPath,
            NuGetFramework framework,
            string configuration)
        {
            var dependencyInfo = _cache.GetDependencyInfo(projectPath, framework, configuration);
            if (dependencyInfo == null)
            {
                return Enumerable.Empty<DependencyDescription>();
            }

            return dependencyInfo.Dependencies;
        }

        public IEnumerable<DiagnosticMessage> GetDependencyDiagnostics(
            string projectPath,
            NuGetFramework framework,
            string configuration)
        {
            var dependencyInfo = _cache.GetDependencyInfo(projectPath, framework, configuration);
            if (dependencyInfo == null)
            {
                return null;
            }

            return dependencyInfo.Diagnostics;
        }

        public IEnumerable<string> GetFileReferences(
            string projectPath,
            NuGetFramework framework,
            string configuration)
        {
            var dependencyInfo = _cache.GetDependencyInfo(projectPath, framework, configuration);
            if (dependencyInfo == null)
            {
                return null;
            }

            return dependencyInfo.FileReferences;
        }

        public IEnumerable<string> GetSources(
            string projectPath,
            NuGetFramework framework,
            string configuration)
        {
            var dependencyInfo = _cache.GetDependencyInfo(projectPath, framework, configuration);
            if (dependencyInfo == null)
            {
                return null;
            }

            var project = _cache.GetProject(projectPath);
            var sources = new List<string>(project.Files.SourceFiles);
            sources.AddRange(dependencyInfo.ExportedSourcesFiles);

            return sources;
        }

        public CommonCompilerOptions GetCompilerOption(
            string projectPath,
            NuGetFramework framework,
            string configuration)
        {
            var project = _cache.GetProject(projectPath);
            if (project == null)
            {
                return null;
            }

            return project.GetCompilerOptions(framework, configuration);
        }
    }
}
