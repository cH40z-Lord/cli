// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;
using Microsoft.Extensions.ProjectModel;
using NuGet.Frameworks;
using Xunit;

namespace Microsoft.DotNet.ProjectModel.Workspace.Tests
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
            // Create a workspace context and resolve all projects.
            // Results must include the both projects found at the path and their dependencies
            var workspaceContext = WorkspaceContext.CreateFrom(_testProjectPath);
            Assert.NotEmpty(workspaceContext.Projects);

            var testProjectPath = workspaceContext.Projects
                                                  .Single(p => p.Contains("ClassLibrary1"));

            var testProject = workspaceContext.GetProject(testProjectPath);

            Assert.Equal("ClassLibrary1", testProject.Name);
            Assert.Equal(2, testProject.GetTargetFrameworks().Count());
            Assert.Equal(2, testProject.GetConfigurations().Count());


            var framework = NuGetFramework.Parse("dnxcore50");
            var configuration = testProject.GetConfigurations().First();

            Assert.NotNull(testProject.GetCompilerOptions(framework, configuration));

            var dependnecyInfo = workspaceContext.GetDependencyInfo(testProject, framework);
            Assert.NotNull(dependnecyInfo);
            Assert.NotEmpty(dependnecyInfo.Dependencies);
            Assert.NotNull(dependnecyInfo.Diagnostics);
            Assert.NotEmpty(dependnecyInfo.FileReferences);
            Assert.NotEmpty(dependnecyInfo.Sources);
            Assert.NotEmpty(dependnecyInfo.ProjectReferences);
        }
    }
}
