using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.ProjectModel;
using Microsoft.Extensions.ProjectModel.Graph;

namespace Microsoft.DotNet.ProjectModel.Workspace
{
    internal class ProjectContextEntry
    {
        private ProjectContext _context;

        public ProjectContext ProjectContext
        {
            get
            {
                return _context;
            }
            set
            {
                if (value != _context)
                {
                    _context = value;
                    UpdateLockFileWriteTime(_context);
                    ResetDependencyInformation();
                }
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
            LastLockFileWriteTime = DateTime.MinValue;
            ResetDependencyInformation();
        }

        public void ResolveDependencies(string configuration)
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
            var exporter = ProjectContext.CreateExporter(configuration);

            foreach (var export in exporter.GetAllExports())
            {
                var library = export.Library;
                var description = CreateDependencyDescription(library, librariesLookup, diagnosticSources[library]);
                Dependencies.Add(description);

                if (library.Identity.Type == LibraryType.Project &&
                    library.Identity.Name != ProjectContext.ProjectFile.Name)
                {
                    var projectLibrary = library as ProjectDescription;
                    if (string.IsNullOrEmpty(projectLibrary.TargetFrameworkInfo?.AssemblyPath) ||
                       !string.IsNullOrEmpty(projectLibrary.TargetFrameworkInfo?.WrappedProject))
                    {
                        string wrappedProjectPath = null;
                        if (!string.IsNullOrEmpty(projectLibrary.TargetFrameworkInfo?.WrappedProject) &&
                             projectLibrary.Project == null)
                        {
                            wrappedProjectPath = Path.GetFullPath(
                                Path.Combine(projectLibrary.Project.ProjectDirectory,
                                             projectLibrary.TargetFrameworkInfo.AssemblyPath));
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

                FileReferences.AddRange(export.RuntimeAssemblies.Select(asset => asset.ResolvedPath));
                SourceFiles.AddRange(export.SourceReferences);
            }

            HasDependencyResolved = true;
        }

        private void ResetDependencyInformation()
        {
            HasDependencyResolved = false;
            Diagnostics.Clear();
            Dependencies.Clear();
            ProjectReferences.Clear();
            FileReferences.Clear();
            SourceFiles.Clear();
        }

        private void UpdateLockFileWriteTime(ProjectContext context)
        {
            if (context == null)
            {
                LastLockFileWriteTime = DateTime.MinValue;
                return;
            }

            var lockFilePath = Path.Combine(context.ProjectFile.ProjectDirectory, LockFile.FileName);
            if (!File.Exists(lockFilePath))
            {
                LastLockFileWriteTime = DateTime.MinValue;
                return;
            }

            LastLockFileWriteTime = File.GetLastWriteTime(lockFilePath);
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

        private static DependencyItem CreateDependencyItem(
            LibraryRange dependency,
            ILookup<string, LibraryDescription> librariesLookup)
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