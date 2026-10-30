namespace FacturAR.Web.Services;

public sealed record SolicitudAltaResultado(bool Exitoso, string? Codigo, string Mensaje)
{
    public static SolicitudAltaResultado ConExito(string mensaje) => new(true, null, mensaje);

    public static SolicitudAltaResultado Fallido(string? codigo, string mensaje) => new(false, codigo, mensaje);
}
