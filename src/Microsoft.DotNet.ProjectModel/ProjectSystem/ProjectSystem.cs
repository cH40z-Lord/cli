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

        private ProjectSystem(GlobalSettings globalSettings)
        {
            // Collect all projects
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

        public ISet<string> GetProjectPaths()
        {
            return _projectPaths;
        }

        /// <summary>
        /// Create a workspace from a given path.
        /// 
        /// The give path could be a global.json file path or a directory contains a 
        /// global.json file. 
        /// </summary>
        /// <param name="path">The path to the workspace.</param>
        /// <returns>Returns workspace instance. If the global.json is missing returns null.</returns>
        public static ProjectSystem Create(string path)
        {
            if (Directory.Exists(path))
            {
                return CreateFromGlobalJson(Path.Combine(path, GlobalSettings.FileName));
            }
            else
            {
                return CreateFromGlobalJson(path);
            }
        }

        /// <summary>
        /// Create a workspace from a global.json.
        /// 
        /// The given path must be a global.json, otherwise null is returned.
        /// </summary>
        /// <param name="filepath">The path to the global.json.</param>
        /// <returns>Returns workspace instance. If a global.json is missing returns null.</returns>
        public static ProjectSystem CreateFromGlobalJson(string filepath)
        {
            if (File.Exists(filepath) &&
                string.Equals(Path.GetFileName(filepath), GlobalSettings.FileName, StringComparison.OrdinalIgnoreCase))
            {
                GlobalSettings globalSettings;
                if (GlobalSettings.TryGetGlobalSettings(filepath, out globalSettings))
                {
                    return new ProjectSystem(globalSettings);
                }
            }

            return null;
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
