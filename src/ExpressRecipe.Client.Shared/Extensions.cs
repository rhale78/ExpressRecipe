using ExpressRecipe.Client.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

public static class IngredientClientExtensions
{
    public static IHostApplicationBuilder AddIngredientClient(this IHostApplicationBuilder builder)
    {
        // Register the concrete typed HttpClient so it is resolvable by its own type.
        // MessagingIngredientServiceClient needs to inject IngredientServiceClient directly
        // as a REST fallback, which only works if the concrete type is registered.
        builder.Services.AddHttpClient<IngredientServiceClient>(client =>
        {
            client.BaseAddress = new Uri("http://ingredientservice");
        });

        // Default interface registration — overridden per-service when messaging is enabled
        // (the last AddSingleton/AddTransient for IIngredientServiceClient wins in ASP.NET Core DI).
        builder.Services.AddTransient<IIngredientServiceClient>(
            sp => sp.GetRequiredService<IngredientServiceClient>());

        // Add gRPC client using Service Discovery
        //builder.Services.AddGrpcClient<IngredientApi.IngredientApiClient>(o =>
        //{
        //    o.Address = new Uri("http://ingredientservice");
        //})
        //.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        //{
        //    // Allow HTTP/1.1 fallback if needed
        //    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        //});

        return builder;
    }

    public static IHostApplicationBuilder AddProductClient(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHttpClient<IProductApiClient, ProductApiClient>(client =>
        {
            client.BaseAddress = new Uri("https+http://productservice");
        });

        return builder;
    }
}
