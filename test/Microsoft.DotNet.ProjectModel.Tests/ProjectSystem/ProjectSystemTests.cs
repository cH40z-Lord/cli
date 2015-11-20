// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;
using Microsoft.Extensions.ProjectModel;
using NuGet.Frameworks;
using Xunit;

namespace Microsoft.DotNet.ProjectModel.ProjectSystem.Tests
{
    public class ProjectSystemTests
    {
        private readonly string _testProjectRoot = "misc";
        private readonly string _testProjectPath;

        public ProjectSystemTests()
        {
            _testProjectPath = Path.Combine(
                ProjectRootResolver.ResolveRootDirectory(Directory.GetCurrentDirectory()),
                _testProjectRoot,
                "WorkspaceTests_Case001");
        }

        [Fact]
        public void CreateFromPath()
        {
            var current = Directory.GetCurrentDirectory();

            var projectSystem = ProjectSystem.Create(_testProjectPath);
            Assert.NotNull(projectSystem);

            var projects = projectSystem.GetProjectPaths();
            Assert.NotNull(projects);
            Assert.NotEmpty(projects);

            var testProjectPath = projects.Where(p => p.Contains("ClassLibrary1")).Single();

            var info = projectSystem.GetProjectInformation(testProjectPath);
            Assert.NotNull(info);
            Assert.Equal("ClassLibrary1", info.Name);
            Assert.Equal(2, info.Frameworks.Count());
            Assert.Equal(2, info.Configurations.Count());

            var framework = NuGetFramework.Parse("dnxcore50");
            var configuration = info.Configurations.First();
            var dependencies = projectSystem.GetDependencies(testProjectPath, framework, configuration);
            Assert.NotEmpty(dependencies);

            var diagnostics = projectSystem.GetDependencyDiagnostics(testProjectPath, framework, configuration);
            Assert.NotNull(diagnostics);

            var fileReferences = projectSystem.GetFileReferences(testProjectPath, framework, configuration);
            Assert.NotEmpty(fileReferences);

            var sources = projectSystem.GetSources(testProjectPath, framework, configuration);
            Assert.NotEmpty(sources);

            var projectReferences = projectSystem.GetProjectReferences(testProjectPath, framework, configuration);
            Assert.NotEmpty(projectReferences);
        }
    }
}
