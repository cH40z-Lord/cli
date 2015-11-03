using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.ProjectModel.Compilation;
using Microsoft.Extensions.ProjectModel.Graph;
using Microsoft.Extensions.ProjectModel.Resolution;

namespace Microsoft.Extensions.ProjectModel.ProjectSystem
{
    internal class DependencyInfoResolver
    {
        private readonly string _configuration;
        private readonly ProjectContext _context;
        private readonly LibraryManager _libraryManager;
        private readonly IList<LibraryDescription> _libraries;
        private readonly ILookup<string, LibraryDescription> _librariesLookup;

        public DependencyInfoResolver(ProjectContext context, string configuration)
        {
            _context = context;
            _libraryManager = context.LibraryManager;

            _libraries = _libraryManager.GetLibraries();
            _librariesLookup = _libraries.ToLookup(lib => lib.Identity.Name);

            _configuration = configuration;
        }

        public DependencyInfo Resolve()
        {
            var diagnostics = _libraryManager.GetAllDiagnostics();
            var dependencies = new List<DependencyDescription>();
            var runtimeAssemblyReferences = new List<string>();
            var projectReferences = new List<ProjectReferenceInfo>();
            var exportedSourceFiles = new List<string>();

            var diagnosticSources = diagnostics.ToLookup(diagnostic => diagnostic.Source);
            ProjectDescription mainProject = null;

            foreach (var library in _libraries)
            {
                // NOTE: stateless, won't be able to tell if a dependency type is changed

                var descrption = CreateDependencyDescription(library, diagnosticSources[library]);
                dependencies.Add(descrption);

                if (library.Identity.Type != LibraryType.Project)
                {
                    continue;
                }

                if (library.Identity.Name == _context.ProjectFile.Name)
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

            var exporter = new LibraryExporter(mainProject, _libraryManager, _configuration);
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
                Dependencies = library.Dependencies.Select(CreateDependencyItem).ToList(),
                Errors = diagnostics.Where(d => d.Severity == DiagnosticMessageSeverity.Error).ToList(),
                Warnings = diagnostics.Where(d => d.Severity == DiagnosticMessageSeverity.Warning).ToList()
            };
        }

        private DependencyItem CreateDependencyItem(LibraryRange dependency)
        {
            var candidates = _librariesLookup[dependency.Name];

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
