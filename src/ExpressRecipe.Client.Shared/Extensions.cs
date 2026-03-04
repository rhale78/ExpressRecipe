using ExpressRecipe.Client.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

public static class IngredientClientExtensions
{
    public static IHostApplicationBuilder AddIngredientClient(this IHostApplicationBuilder builder)
    {
        // Add HttpClient for the Ingredient service using Service Discovery
        builder.Services.AddHttpClient<IIngredientServiceClient, IngredientServiceClient>(client =>
        {
            client.BaseAddress = new Uri("http://ingredientservice");
        });

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
