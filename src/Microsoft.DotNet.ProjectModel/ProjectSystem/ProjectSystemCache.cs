// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.ProjectModel;
using Microsoft.Extensions.ProjectModel.Compilation;
using Microsoft.Extensions.ProjectModel.Graph;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectModel.ProjectSystem
{
    internal class ProjectSystemCache
    {
        private Cache Cache { get; } = new Cache();

        public Project GetProject(string projectPath)
        {
            return (Project)Cache.Get(Tuple.Create(typeof(Project), projectPath), ctx =>
            {
                Cache.TriggerDependency(projectPath);
                Cache.MonitorFile(ctx, projectPath);

                Project project;
                if (!ProjectReader.TryGetProject(projectPath, out project))
                {
                    return null;
                }
                else
                {
                    return project;
                }
            });
        }

        public ProjectContext GetProjectContext(string projectPath, NuGetFramework framework)
        {
            var project = GetProject(projectPath);
            if (project == null)
            {
                return null;
            }

            return (ProjectContext)Cache.Get(Tuple.Create(typeof(ProjectContext), projectPath, framework), ctx =>
            {
                Cache.TriggerDependency(projectPath, framework.Framework);
                Cache.MonitorDependency(ctx, projectPath);

                var builder = new ProjectContextBuilder()
                    .WithProject(project)
                    .WithTargetFramework(framework);

                return builder.Build();
            });
        }

        public DependencyInfo GetDependencyInfo(string projectPath, NuGetFramework framework, string configuration)
        {
            var projectContext = GetProjectContext(projectPath, framework);
            if (projectContext == null)
            {
                return null;
            }

            var cacheKey = Tuple.Create(typeof(DependencyInfo), projectPath, framework, configuration);
            return Cache.Get(cacheKey, ctx =>
            {
                Cache.TriggerDependency(projectPath, framework.Framework, configuration);
                Cache.MonitorDependency(ctx, projectPath, framework.Framework);

                //var resolver = new DependencyInfoResolver(projectContext, configuration);
                return Resolve(projectContext, configuration);
            }) as DependencyInfo;
        }

        private DependencyInfo Resolve(ProjectContext projectContext, string configuration)
        {
            var diagnostics = projectContext.LibraryManager.GetAllDiagnostics();
            var libraries = projectContext.LibraryManager.GetLibraries();
            var librariesLookup = libraries.ToLookup(lib => lib.Identity.Name);
            var dependencies = new List<DependencyDescription>();
            var runtimeAssemblyReferences = new List<string>();
            var projectReferences = new List<ProjectReferenceInfo>();
            var exportedSourceFiles = new List<string>();

            var diagnosticSources = diagnostics.ToLookup(diagnostic => diagnostic.Source);
            ProjectDescription mainProject = null;

            foreach (var library in libraries)
            {
                // NOTE: stateless, won't be able to tell if a dependency type is changed

                var descrption = CreateDependencyDescription(library, librariesLookup, diagnosticSources[library]);
                dependencies.Add(descrption);

                if (library.Identity.Type != LibraryType.Project)
                {
                    continue;
                }

                if (library.Identity.Name == projectContext.ProjectFile.Name)
                {
                    mainProject = (ProjectDescription)library;
                    continue;
                }

                // resolving project reference
                var projectLibrary = (ProjectDescription)library;
                var targetFrameworkInformation = projectLibrary.TargetFrameworkInfo;

                // if this is an assembly reference then treat it like a file reference
                if (!string.IsNullOrEmpty(targetFrameworkInformation?.AssemblyPath) &&
                     string.IsNullOrEmpty(targetFrameworkInformation?.WrappedProject))
                {
                    var assemblyPath = Path.GetFullPath(
                        Path.Combine(projectLibrary.Project.ProjectDirectory,
                                     targetFrameworkInformation.AssemblyPath));
                    runtimeAssemblyReferences.Add(assemblyPath);
                }
                else
                {
                    string wrappedProjectPath = null;
                    if (!string.IsNullOrEmpty(targetFrameworkInformation?.WrappedProject) &&
                         projectLibrary.Project == null)
                    {
                        wrappedProjectPath = Path.GetFullPath(
                            Path.Combine(projectLibrary.Project.ProjectDirectory,
                                         targetFrameworkInformation.AssemblyPath));
                    }

                    projectReferences.Add(new ProjectReferenceInfo
                    {
                        Name = library.Identity.Name,
                        Framework = library.Framework,
                        Path = library.Path,
                        WrappedProjectPath = wrappedProjectPath
                    });
                }
            }

            var exporter = new LibraryExporter(mainProject, projectContext.LibraryManager, configuration);
            var exports = exporter.GetAllExports();

            foreach (var export in exporter.GetAllExports())
            {
                runtimeAssemblyReferences.AddRange(export.CompilationAssemblies.Select(asset => asset.ResolvedPath));
                exportedSourceFiles.AddRange(export.SourceReferences);
            }

            return new DependencyInfo(diagnostics,
                                      dependencies,
                                      projectReferences,
                                      runtimeAssemblyReferences,
                                      exportedSourceFiles);
        }

        private DependencyDescription CreateDependencyDescription(
            LibraryDescription library,
            ILookup<string, LibraryDescription> librariesLookup,
            IEnumerable<DiagnosticMessage> diagnostics)
        {
            return new DependencyDescription
            {
                Name = library.Identity.Name,
                // TODO: Correct? Different from DTH
                DisplayName = library.Identity.Name,
                Version = library.Identity.Version?.ToString(),
                Type = library.Identity.Type.Value,
                Resolved = library.Resolved,
                Path = library.Path,
                Dependencies = library.Dependencies.Select(dep=> CreateDependencyItem(dep, librariesLookup)).ToList(),
                Errors = diagnostics.Where(d => d.Severity == DiagnosticMessageSeverity.Error).ToList(),
                Warnings = diagnostics.Where(d => d.Severity == DiagnosticMessageSeverity.Warning).ToList()
            };
        }

        private DependencyItem CreateDependencyItem(LibraryRange dependency, ILookup<string, LibraryDescription> librariesLookup)
        {
            var candidates = librariesLookup[dependency.Name];

            LibraryDescription result;
            if (dependency.Target == LibraryType.Unspecified)
            {
                result = candidates.FirstOrDefault();
            }
            else
            {
                result = candidates.FirstOrDefault(cand => cand.Identity.Type == dependency.Target);
            }

            if (result != null)
            {
                return new DependencyItem { Name = dependency.Name, Version = result.Identity.Version.ToString() };
            }
            else
            {
                return new DependencyItem { Name = dependency.Name };
            }
        }
    }
}
