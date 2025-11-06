using Microsoft.Extensions.DependencyInjection;
using PublicTxt.Core.Git;

namespace PublicTxt.Git;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGitInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IGitRepositoryService, LibGit2SharpRepositoryService>();

        return services;
    }
}
