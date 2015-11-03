using System;
using System.Threading.Tasks;
using Microsoft.DotNet.ProjectModel.Caching;
using NuGet.Frameworks;

namespace Microsoft.Extensions.ProjectModel.ProjectSystem
{
    internal class ProjectSystemCache
    {
        public ProjectSystemCache()
        {
            CacheContextAccessor = new CacheContextAccessor();
            Cache = new Cache(CacheContextAccessor);
            NamedCacheDependencyProvider = new NamedCacheDependencyProvider();
        }

        public ICacheContextAccessor CacheContextAccessor { get; }

        public ICache Cache { get; }

        public INamedCacheDependencyProvider NamedCacheDependencyProvider { get; }

        public Task<Project> GetProjectAsync(string projectPath)
        {
            return Task.Run(() =>
            {
                return (Project)Cache.Get(Tuple.Create(typeof(Project), projectPath), ctx =>
                {
                    //_logger.LogVerbose($"Reading project {projectHandle} => {projectPath}");

                    TriggerDependency<Project>(projectPath);
                    MonitorDependency(ctx, new FileWriteTimeCacheDependency(projectPath));

                    Project project;
                    if (!ProjectReader.TryGetProject(projectPath, out project))
                    {
                        return null;
                    }
                    else
                    {
                        return project;
                    }
                });
            });
        }

        public async Task<ProjectContext> GetProjectContext(string projectPath, NuGetFramework framework)
        {
            var project = await GetProjectAsync(projectPath);
            if (project == null)
            {
                return null;
            }

            var projectContext = await Task.Run(() =>
            {
                return Cache.Get(Tuple.Create(typeof(ProjectContext), projectPath, framework), ctx =>
                {
                    //_logger.LogVerbose($"Reading project context {projectHandle} => {project.ProjectDirectory}");
                    TriggerDependency<ProjectContext>(projectPath, framework);
                    MonitorDependency<Project>(ctx, projectPath);

                    var builder = new ProjectContextBuilder()
                        .WithProject(project)
                        .WithTargetFramework(framework);

                    return builder.Build();
                }) as ProjectContext;
            });

            return projectContext;
        }

        public async Task<DependencyInfo> GetDependencyInfo(string projectPath, NuGetFramework framework, string configuration)
        {
            var projectContext = await GetProjectContext(projectPath, framework);
            if (projectContext == null)
            {
                return null;
            }

            var cacheKey = Tuple.Create(typeof(DependencyInfo), projectPath, framework, configuration);
            return Cache.Get(cacheKey, ctx =>
            {
                //_logger.LogVerbose($"Resolving dependencies for project {projectHandle}/{framework}");

                TriggerDependency<DependencyInfo>(projectPath, framework, configuration);
                MonitorDependency<ProjectContext>(ctx, projectPath, framework);

                var resolver = new DependencyInfoResolver(projectContext, configuration);
                return resolver.Resolve();
            }) as DependencyInfo;
        }

        private void TriggerDependency<T>(string projectPath)
        {
            NamedCacheDependencyProvider.Trigger(GetCacheDependencyName<T>(projectPath));
        }

        private void TriggerDependency<T>(string projectPath, NuGetFramework framework)
        {
            NamedCacheDependencyProvider.Trigger(GetCacheDependencyName<T>(projectPath, framework));
        }

        private void TriggerDependency<T>(string projectPath, NuGetFramework framework, string configuration)
        {
            NamedCacheDependencyProvider.Trigger(GetCacheDependencyName<T>(projectPath, framework, configuration));
        }

        private void MonitorDependency(CacheContext ctx, ICacheDependency dependency)
        {
            ctx.Monitor(dependency);
        }

        private void MonitorDependency<T>(CacheContext ctx, string projectPath)
        {
            MonitorDependency(ctx,
                NamedCacheDependencyProvider.GetNamedDependency(GetCacheDependencyName<T>(projectPath)));
        }

        private void MonitorDependency<T>(CacheContext ctx, string projectPath, NuGetFramework framework)
        {
            MonitorDependency(ctx,
                NamedCacheDependencyProvider.GetNamedDependency(GetCacheDependencyName<T>(projectPath, framework)));
        }

        private string GetCacheDependencyName<T>(string projectPath)
        {
            return $"DependencyName_of_ProjectPath_{projectPath}_for_{typeof(T).Name}";
        }

        private string GetCacheDependencyName<T>(string projectPath, NuGetFramework framework)
        {
            return $"DependencyName_of_ProjectPath_{projectPath}_for_{typeof(T).Name}_{framework.GetShortFolderName()}";
        }

        private string GetCacheDependencyName<T>(string projectPath, NuGetFramework framework, string configuration)
        {
            return $"DependencyName_of_ProjectPath_{projectPath}_for_{typeof(T).Name}_{framework.GetShortFolderName()}_{configuration}";
        }
    }
}
