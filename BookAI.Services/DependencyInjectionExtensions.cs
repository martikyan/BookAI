using System.ClientModel;
using BookAI.Services.Abstraction;
using BookAI.Services.Models;
using BookAI.Services.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace BookAI.Services;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OpenAIOptions>()
            .BindConfiguration("OpenAI");

        services.AddSingleton<IHtmlService, HtmlService>();
        services.AddScoped<EpubService>();
        services.AddScoped<IAIService, AIService>();
        services.AddScoped<EndnoteSequenceProvider>();
        services.AddSingleton<ICalibreService, CalibreService>();

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<OpenAIOptions>>();

            ChatClient client = new(options.Value.Model, new ApiKeyCredential(options.Value.ApiKey));

            return client;
        });

        return services;
    }
}