using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Frameworks;

namespace Microsoft.Extensions.ProjectModel.ProjectSystem
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

        public async Task<ProjectInformation> GetProjectInformationAsync(string projectPath)
        {
            var project = await _cache.GetProjectAsync(projectPath);
            if (project == null)
            {
                return null;
            }
            else
            {
                return new ProjectInformation(project);
            }
        }

        public async Task<IEnumerable<ProjectReferenceInfo>> GetProjectReferencesAsync(
            string projectPath,
            NuGetFramework framework,
            string configuration)
        {
            var dependencyInfo = await _cache.GetDependencyInfo(projectPath, framework, configuration);
            if (dependencyInfo == null)
            {
                return Enumerable.Empty<ProjectReferenceInfo>();
            }

            return dependencyInfo.ProjectReferences;
        }

        public async Task<IEnumerable<DependencyDescription>> GetDependenciesAsync(
            string projectPath,
            NuGetFramework framework,
            string configuration)
        {
            var dependencyInfo = await _cache.GetDependencyInfo(projectPath, framework, configuration);
            if (dependencyInfo == null)
            {
                return Enumerable.Empty<DependencyDescription>();
            }

            return dependencyInfo.Dependencies;
        }

        public async Task<IEnumerable<DiagnosticMessage>> GetDependencyDiagnosticsAsync(
            string projectPath,
            NuGetFramework framework,
            string configuration)
        {
            var dependencyInfo = await _cache.GetDependencyInfo(projectPath, framework, configuration);
            if (dependencyInfo == null)
            {
                return null;
            }

            return dependencyInfo.Diagnostics;
        }

        public async Task<IEnumerable<string>> GetFileReferencesAsync(
            string projectPath,
            NuGetFramework framework,
            string configuration)
        {
            var dependencyInfo = await _cache.GetDependencyInfo(projectPath, framework, configuration);
            if (dependencyInfo == null)
            {
                return null;
            }

            return dependencyInfo.FileReferences;
        }

        public async Task<IEnumerable<string>> GetSourcesAsync(
            string projectPath,
            NuGetFramework framework,
            string configuration)
        {
            var dependencyInfo = await _cache.GetDependencyInfo(projectPath, framework, configuration);
            if (dependencyInfo == null)
            {
                return null;
            }

            var project = await _cache.GetProjectAsync(projectPath);
            var sources = new List<string>(project.Files.SourceFiles);
            sources.AddRange(dependencyInfo.ExportedSourcesFiles);

            return sources;
        }

        public async Task<CommonCompilerOptions> GetCompilerOptionAsync(
            string projectPath,
            NuGetFramework framework,
            string configuration)
        {
            var project = await _cache.GetProjectAsync(projectPath);
            if (project == null)
            {
                return null;
            }

            return project.GetCompilerOptions(framework, configuration);
        }
    }
}
