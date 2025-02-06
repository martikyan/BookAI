using System.ClientModel;
using BookAI.Services.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Data;
using OpenAI;
using OpenAI.Chat;

namespace BookAI.Services;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OpenAIOptions>()
            .Bind(configuration.GetSection("OpenAI"));

        services.AddSingleton<HtmlService>();
        services.AddSingleton<EpubService>();
        services.AddSingleton<AIService>();
        services.AddSingleton<EndnoteSequence>();

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<OpenAIOptions>>();

            ChatClient client = new(options.Value.Model, new ApiKeyCredential(options.Value.ApiKey));

            return client;
        });

        return services;
    }
}