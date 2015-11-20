// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Extensions.ProjectModel;

namespace Microsoft.DotNet.ProjectModel.ProjectSystem
{
    internal class DependencyInfo
    {
        public DependencyInfo(IList<DiagnosticMessage> diagnostics,
                              IList<DependencyDescription> dependencies,
                              IList<ProjectReferenceInfo> projectReferences,
                              IList<string> runtimeAssemblyReferences,
                              IList<string> exportedSourceFiles)
        {
            Diagnostics = diagnostics;
            Dependencies = dependencies;
            FileReferences = runtimeAssemblyReferences;
            ProjectReferences = projectReferences;
            ExportedSourcesFiles = exportedSourceFiles;
        }

        public IList<DiagnosticMessage> Diagnostics { get; }

        public IList<DependencyDescription> Dependencies { get; }

        public IList<string> FileReferences { get; }

        public IList<ProjectReferenceInfo> ProjectReferences { get; }

        public IList<string> ExportedSourcesFiles { get; }

        //public Dictionary<string, byte[]> RawReferences { get; set; } = new Dictionary<string, byte[]>();
    }
}
