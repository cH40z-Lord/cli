using NuGet.Frameworks;

namespace Microsoft.Extensions.ProjectModel.ProjectSystem
{
    public class ProjectReferenceInfo
    {
        public NuGetFramework Framework { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public string WrappedProjectPath { get; set; }
    }
}