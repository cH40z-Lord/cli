namespace Microsoft.DotNet.ProjectModel.Caching
{
    internal interface ICacheContextAccessor
    {
        CacheContext Current { get; set; }
    }
}
