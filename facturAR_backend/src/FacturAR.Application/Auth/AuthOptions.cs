namespace FacturAR.Application.Auth;

public sealed class AuthOptions
{
    public AuthCookieSettings Cookie { get; set; } = new();
    public int PasswordHashIterations { get; set; } = 600000;
    public AuthLoginSettings Login { get; set; } = new();

    public sealed class AuthCookieSettings
    {
        public string Dominio { get; set; } = string.Empty;
        public string NombreContribuyente { get; set; } = ".FacturAR.Contribuyente";
        public string NombreAdmin { get; set; } = ".FacturAR.Admin";
        public int HorasSesionEmpresa { get; set; } = 8;
        public int HorasSesionAdmin { get; set; } = 2;
    }

    public sealed class AuthLoginSettings
    {
        public int MaxIntentosFallidos { get; set; } = 5;
        public int MinutosBloqueo { get; set; } = 15;
        public int RateLimitPorIp { get; set; } = 10;
        public int VentanaRateLimitMinutos { get; set; } = 5;
    }
}