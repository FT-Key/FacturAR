using SoapCore;
using SoapCore.Extensibility;

namespace FacturAR.Api.Soap;

public static class SoapServiceExtensions
{
    public static IServiceCollection AddFacturarSoap(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddSoapCore();
        services.AddScoped<ContractFecae>();
        services.AddScoped<IContractFecae>(sp => sp.GetRequiredService<ContractFecae>());
        services.AddSingleton<IFaultExceptionTransformer, FecaeFaultExceptionTransformer>();

        return services;
    }

    public static IApplicationBuilder UseFacturarSoap(this IApplicationBuilder app, SoapOptions options)
    {
        if (!options.Habilitado)
        {
            return app;
        }

        return app.UseSoapEndpoint<IContractFecae>(
            options.Ruta,
            new SoapEncoderOptions(),
            SoapSerializer.XmlSerializer);
    }
}
