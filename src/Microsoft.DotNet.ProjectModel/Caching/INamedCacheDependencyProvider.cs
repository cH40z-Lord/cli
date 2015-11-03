namespace Microsoft.DotNet.ProjectModel.Caching
{
    internal interface INamedCacheDependencyProvider
    {
        ICacheDependency GetNamedDependency(string name);

        void Trigger(string name);
    }
}
