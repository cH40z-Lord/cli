// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.Generic;
using Microsoft.Extensions.ProjectModel;

namespace Microsoft.DotNet.ProjectModel.Workspace
{
    public class DependencyInfo
    {
        public DependencyInfo(IList<DiagnosticMessage> diagnostics,
                              IList<DependencyDescription> dependencies,
                              IList<ProjectReferenceInfo> projectReferences,
                              IList<string> runtimeAssemblyReferences,
                              IList<string> sources)
        {
            Diagnostics = diagnostics;
            Dependencies = dependencies;
            FileReferences = runtimeAssemblyReferences;
            ProjectReferences = projectReferences;
            Sources = sources;
        }

        public IList<DiagnosticMessage> Diagnostics { get; }

        public IList<DependencyDescription> Dependencies { get; }

        public IList<string> FileReferences { get; }

        public IList<ProjectReferenceInfo> ProjectReferences { get; }

        public IList<string> Sources { get; }
    }
}
