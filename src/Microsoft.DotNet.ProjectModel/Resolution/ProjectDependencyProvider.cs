// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.ProjectModel.Graph;
using NuGet.Frameworks;

namespace Microsoft.Extensions.ProjectModel.Resolution
{
    public class ProjectDependencyProvider
    {
        private Func<string, Project> _resolveProject;

        public ProjectDependencyProvider()
        {
            _resolveProject = ResolveProject;
        }

        public ProjectDependencyProvider(Func<string, Project> projectCacheResolver)
        {
            _resolveProject = projectCacheResolver;
        }

        public ProjectDescription GetDescription(string name,
                                                 string path,
                                                 LockFileTargetLibrary targetLibrary,
                                                 Func<string, Project> projectCacheResolver)
        {
            var project = _resolveProject(Path.GetDirectoryName(path));
            if (project != null)
            {
                return GetDescription(targetLibrary.TargetFramework, project);
            }
            else
            {
                return new ProjectDescription(name, path);
            }
        }

        public ProjectDescription GetDescription(string name, string path, LockFileTargetLibrary targetLibrary)
        {
            return GetDescription(name, path, targetLibrary, projectCacheResolver: null);
        }

        public ProjectDescription GetDescription(NuGetFramework targetFramework, Project project)
        {
            // This never returns null
            var targetFrameworkInfo = project.GetTargetFramework(targetFramework);
            var targetFrameworkDependencies = new List<LibraryRange>(targetFrameworkInfo.Dependencies);

            if (targetFramework != null && targetFramework.IsDesktop())
            {
                targetFrameworkDependencies.Add(new LibraryRange("mscorlib", LibraryType.ReferenceAssembly));

                targetFrameworkDependencies.Add(new LibraryRange("System", LibraryType.ReferenceAssembly));

                if (targetFramework.Version >= new Version(3, 5))
                {
                    targetFrameworkDependencies.Add(new LibraryRange("System.Core", LibraryType.ReferenceAssembly));

                    if (targetFramework.Version >= new Version(4, 0))
                    {
                        targetFrameworkDependencies.Add(new LibraryRange("Microsoft.CSharp", LibraryType.ReferenceAssembly));
                    }
                }
            }

            var dependencies = project.Dependencies.Concat(targetFrameworkDependencies).ToList();

            // Mark the library as unresolved if there were specified frameworks
            // and none of them resolved
            bool unresolved = targetFrameworkInfo.FrameworkName == null;

            return new ProjectDescription(
                new LibraryRange(project.Name, LibraryType.Unspecified),
                project,
                dependencies,
                targetFrameworkInfo,
                !unresolved);
        }

        private Project ResolveProject(string path)
        {
            Project project;
            if (ProjectReader.TryGetProject(path, out project))
            {
                return project;
            }
            else
            {
                return null;
            }
        }
    }
}
