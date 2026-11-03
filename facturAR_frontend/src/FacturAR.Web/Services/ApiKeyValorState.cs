namespace FacturAR.Web.Services;

public sealed class ApiKeyValorState
{
    public string? Nombre { get; private set; }
    public string? Valor { get; private set; }
    public bool HayValor => !string.IsNullOrEmpty(Valor);
    public void Mostrar(string nombre, string valor) { Nombre = nombre; Valor = valor; }
}
