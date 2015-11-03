using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.ProjectModel;
using Microsoft.Extensions.ProjectModel.ProjectSystem;
using NuGet.Frameworks;
using Xunit;

namespace Microsoft.DotNet.ProjectModel.Workspaces.Tests
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
        public async Task CreateFromPath()
        {
            var current = Directory.GetCurrentDirectory();

            var projectSystem = ProjectSystem.Create(_testProjectPath);
            Assert.NotNull(projectSystem);

            var projects = projectSystem.GetProjectPaths();
            Assert.NotNull(projects);
            Assert.NotEmpty(projects);

            var testProjectPath = projects.Where(p => p.Contains("ClassLibrary1")).Single();

            var info = await projectSystem.GetProjectInformationAsync(testProjectPath);
            Assert.NotNull(info);
            Assert.Equal("ClassLibrary1", info.Name);
            Assert.Equal(2, info.Frameworks.Count());
            Assert.Equal(2, info.Configurations.Count());

            var framework = NuGetFramework.Parse("dnxcore50");
            var configuration = info.Configurations.First();
            var dependencies = await projectSystem.GetDependenciesAsync(testProjectPath, framework, configuration);
            Assert.NotEmpty(dependencies);

            var diagnostics = await projectSystem.GetDependencyDiagnosticsAsync(testProjectPath, framework, configuration);
            Assert.NotNull(diagnostics);

            var fileReferences = await projectSystem.GetFileReferencesAsync(testProjectPath, framework, configuration);
            Assert.NotEmpty(fileReferences);

            var sources = await projectSystem.GetSourcesAsync(testProjectPath, framework, configuration);
            Assert.NotEmpty(sources);

            var projectReferences = await projectSystem.GetProjectReferencesAsync(testProjectPath, framework, configuration);
            Assert.NotEmpty(projectReferences);
        }
    }
}
