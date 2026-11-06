namespace FacturAR.Web.Services.Asistente;

public sealed class AsistenteOptions
{
    public const string SectionName = "Asistente";

    public bool Habilitado { get; set; }

    public string? GroqApiKey { get; set; }

    public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1";

    public string Modelo { get; set; } = "llama-3.3-70b-versatile";

    public int TimeoutSegundos { get; set; } = 10;

    public int MaxTokensRespuesta { get; set; } = 350;

    public int MaxCaracteresMensaje { get; set; } = 600;

    public int MaxTurnosHistorial { get; set; } = 8;

    public int RatePorMinutoPorIp { get; set; } = 6;

    public int RatePorHoraPorIp { get; set; } = 30;

    public int RatePorDiaPorIp { get; set; } = 100;

    public int RatePorHoraPorSesion { get; set; } = 20;

    public int PresupuestoDiarioTokens { get; set; } = 150000;

    public int MaxRequestsPorDia { get; set; } = 500;

    public string? OrigenPermitido { get; set; }

    public static IDictionary<string, string?> VariablesEntorno()
    {
        var variables = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        Agregar(variables, "ASISTENTE_HABILITADO", $"{SectionName}:Habilitado");
        Agregar(variables, "GROQ_API_KEY", $"{SectionName}:GroqApiKey");
        Agregar(variables, "ASISTENTE_BASE_URL", $"{SectionName}:BaseUrl");
        Agregar(variables, "ASISTENTE_MODELO", $"{SectionName}:Modelo");
        Agregar(variables, "ASISTENTE_TIMEOUT_SEGUNDOS", $"{SectionName}:TimeoutSegundos");
        Agregar(variables, "ASISTENTE_MAX_TOKENS", $"{SectionName}:MaxTokensRespuesta");
        Agregar(variables, "ASISTENTE_MAX_CARACTERES_MENSAJE", $"{SectionName}:MaxCaracteresMensaje");
        Agregar(variables, "ASISTENTE_MAX_TURNOS", $"{SectionName}:MaxTurnosHistorial");
        Agregar(variables, "ASISTENTE_RATE_MINUTO", $"{SectionName}:RatePorMinutoPorIp");
        Agregar(variables, "ASISTENTE_RATE_HORA", $"{SectionName}:RatePorHoraPorIp");
        Agregar(variables, "ASISTENTE_RATE_DIA", $"{SectionName}:RatePorDiaPorIp");
        Agregar(variables, "ASISTENTE_RATE_SESION", $"{SectionName}:RatePorHoraPorSesion");
        Agregar(variables, "ASISTENTE_PRESUPUESTO_TOKENS", $"{SectionName}:PresupuestoDiarioTokens");
        Agregar(variables, "ASISTENTE_MAX_REQUESTS_DIA", $"{SectionName}:MaxRequestsPorDia");
        Agregar(variables, "ASISTENTE_ORIGEN", $"{SectionName}:OrigenPermitido");

        return variables;
    }

    private static void Agregar(IDictionary<string, string?> variables, string variable, string clave)
    {
        var valor = Environment.GetEnvironmentVariable(variable);
        if (!string.IsNullOrWhiteSpace(valor))
        {
            variables[clave] = valor;
        }
    }
}
