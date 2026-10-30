using System.Net;

namespace FacturAR.Web.Services;

public sealed record ResultadoApi<T>(bool Exitoso, T? Valor, HttpStatusCode Status, string? Codigo, string? Mensaje)
{
    public bool SinSesion => Status is HttpStatusCode.Unauthorized;

    public static ResultadoApi<T> ConExito(T valor) => new(true, valor, HttpStatusCode.OK, null, null);

    public static ResultadoApi<T> Fallido(HttpStatusCode status, string? codigo, string mensaje)
        => new(false, default, status, codigo, mensaje);
}
