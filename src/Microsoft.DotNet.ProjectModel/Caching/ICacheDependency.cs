namespace Microsoft.DotNet.ProjectModel.Caching
{
    internal interface ICacheDependency
    {
        bool HasChanged { get; }
    }
}