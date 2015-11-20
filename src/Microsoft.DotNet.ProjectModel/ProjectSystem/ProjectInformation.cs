// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.ProjectModel;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectModel.ProjectSystem
{
    public class ProjectInformation
    {
        public ProjectInformation(Project project)
        {
            ResolveSearchPaths(project);
            Name = project.Name;
            Commands = project.Commands;
            Configurations = project.GetConfigurations();
            Frameworks = project.GetTargetFrameworks().Select(tf => tf.FrameworkName);
        }

        public string Name { get; }

        public IDictionary<string, string> Commands { get; }

        public IEnumerable<string> Configurations { get; }

        public IEnumerable<NuGetFramework> Frameworks { get; }

        public IEnumerable<string> ProjectSearchPaths { get; private set; }

        public string GlobalJsonPath { get; private set; }

        private void ResolveSearchPaths(Project project)
        {
            var searchPaths = new HashSet<string>();
            searchPaths.Add(Directory.GetParent(project.ProjectDirectory).FullName);

            GlobalSettings settings = null;

            var root = ProjectRootResolver.ResolveRootDirectory(project.ProjectDirectory);
            if (GlobalSettings.TryGetGlobalSettings(root, out settings))
            {
                GlobalJsonPath = settings.FilePath;

                foreach (var searchPath in settings.ProjectSearchPaths)
                {
                    var path = Path.Combine(settings.DirectoryPath, searchPath);
                    searchPaths.Add(Path.GetFullPath(path));
                }
            }
            else
            {
                GlobalJsonPath = string.Empty;
            }

            ProjectSearchPaths = searchPaths.ToList();
        }
    }
}