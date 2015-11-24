using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.ProjectModel;
using Microsoft.Extensions.ProjectModel.Compilation;
using Microsoft.Extensions.ProjectModel.Graph;

namespace Microsoft.DotNet.ProjectModel.Workspace
{
    internal class ProjectContextEntry
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

                Diagnostics.Clear();
                Dependencies.Clear();
                ProjectReferences.Clear();
                FileReferences.Clear();
                SourceFiles.Clear();
                HasDependencyResolved = false;
                _context = value;
            }
        }

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

        public bool HasDependencyResolved { get; private set; }

        public List<DiagnosticMessage> Diagnostics { get; } = new List<DiagnosticMessage>();

        public List<DependencyDescription> Dependencies { get; } = new List<DependencyDescription>();

        public List<ProjectReferenceInfo> ProjectReferences { get; } = new List<ProjectReferenceInfo>();

        public List<string> FileReferences { get; } = new List<string>();

        public List<string> SourceFiles { get; } = new List<string>();

        public void Reset()
        {
            _context = null;
            HasDependencyResolved = false;

            Diagnostics.Clear();
            Dependencies.Clear();
            ProjectReferences.Clear();
            FileReferences.Clear();
            SourceFiles.Clear();

            LastLockFileWriteTime = DateTime.MinValue;
        }

        public void GetDependencyInfo(string configuration)
        {
            Diagnostics.Clear();
            Dependencies.Clear();
            ProjectReferences.Clear();
            FileReferences.Clear();
            SourceFiles.Clear();

            Diagnostics.AddRange(ProjectContext.LibraryManager.GetAllDiagnostics());
            SourceFiles.AddRange(ProjectContext.ProjectFile.Files.SourceFiles);

            var libraries = ProjectContext.LibraryManager.GetLibraries();
            var librariesLookup = libraries.ToLookup(lib => lib.Identity.Name);
            var diagnosticSources = Diagnostics.ToLookup(diagnostic => diagnostic.Source);
            ProjectDescription mainProject = null;

            foreach (var library in libraries)
            {
                // NOTE: stateless, won't be able to tell if a dependency type is changed

                var descrption = CreateDependencyDescription(library, librariesLookup, diagnosticSources[library]);
                Dependencies.Add(descrption);

                if (library.Identity.Type != LibraryType.Project)
                {
                    continue;
                }

                if (library.Identity.Name == ProjectContext.ProjectFile.Name)
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
                    FileReferences.Add(assemblyPath);
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

                    ProjectReferences.Add(new ProjectReferenceInfo
                    {
                        Name = library.Identity.Name,
                        Framework = library.Framework,
                        Path = library.Path,
                        WrappedProjectPath = wrappedProjectPath
                    });
                }
            }

            var exporter = new LibraryExporter(mainProject, ProjectContext.LibraryManager, configuration);
            var exports = exporter.GetAllExports();

            foreach (var export in exporter.GetAllExports())
            {
                FileReferences.AddRange(export.CompilationAssemblies.Select(asset => asset.ResolvedPath));
                SourceFiles.AddRange(export.SourceReferences);
            }

            HasDependencyResolved = true;
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