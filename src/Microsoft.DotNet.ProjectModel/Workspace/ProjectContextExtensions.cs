using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.ProjectModel;
using Microsoft.Extensions.ProjectModel.Compilation;
using Microsoft.Extensions.ProjectModel.Graph;

namespace Microsoft.DotNet.ProjectModel.Workspace
{
    public static class ProjectContextExtensions
    {
        public static DependencyInfo GetDependencyInfo(this ProjectContext projectContext, string configuration)
        {
            var diagnostics = projectContext.LibraryManager.GetAllDiagnostics();
            var libraries = projectContext.LibraryManager.GetLibraries();
            var librariesLookup = libraries.ToLookup(lib => lib.Identity.Name);
            var dependencies = new List<DependencyDescription>();
            var runtimeAssemblyReferences = new List<string>();
            var projectReferences = new List<ProjectReferenceInfo>();
            var sourceFiles = new List<string>(projectContext.ProjectFile.Files.SourceFiles);

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
                sourceFiles.AddRange(export.SourceReferences);
            }

            return new DependencyInfo(diagnostics,
                                      dependencies,
                                      projectReferences,
                                      runtimeAssemblyReferences,
                                      sourceFiles);
        }

        private static DependencyDescription CreateDependencyDescription(
            LibraryDescription library,
            ILookup<string, LibraryDescription> librariesLookup,
            IEnumerable<DiagnosticMessage> diagnostics)
        {
            return new DependencyDescription
            {
                Name = library.Identity.Name,
                Version = library.Identity.Version?.ToString(),
                Type = library.Identity.Type.Value,
                Resolved = library.Resolved,
                Path = library.Path,
                Dependencies = library.Dependencies.Select(dep => CreateDependencyItem(dep, librariesLookup)).ToList(),
                Errors = diagnostics.Where(d => d.Severity == DiagnosticMessageSeverity.Error).ToList(),
                Warnings = diagnostics.Where(d => d.Severity == DiagnosticMessageSeverity.Warning).ToList()
            };
        }

        private static DependencyItem CreateDependencyItem(LibraryRange dependency, ILookup<string, LibraryDescription> librariesLookup)
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
