using System.Collections.Generic;

namespace Microsoft.Extensions.ProjectModel.ProjectSystem
{
    public class DependencyDescription
    {
        public string Name { get; set; }

        public string DisplayName { get; set; }

        public string Version { get; set; }

        public string Path { get; set; }

        public string Type { get; set; }

        public bool Resolved { get; set; }

        public IEnumerable<DependencyItem> Dependencies { get; set; }

        public IEnumerable<DiagnosticMessage> Errors { get; set; }

        public IEnumerable<DiagnosticMessage> Warnings { get; set; }
    }
}
