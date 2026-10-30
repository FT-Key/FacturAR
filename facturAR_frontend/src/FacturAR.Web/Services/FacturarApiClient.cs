using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FacturAR.Contracts.Auth;
using FacturAR.Contracts.Comunes;
using FacturAR.Contracts.Comprobantes;
using FacturAR.Contracts.Invitaciones;
using FacturAR.Contracts.Solicitudes;

namespace FacturAR.Web.Services;

public sealed class FacturarApiClient(HttpClient httpClient)
{
    private const string CookieHeader = "Cookie";
    private const string SetCookieHeader = "Set-Cookie";
    private const string MensajeSolicitudRecibida = "Recibimos tu solicitud.";
    private const string MensajeErrorSolicitud = "No pudimos enviar la solicitud. Intentá de nuevo en unos minutos.";
    private const string MensajeApiNoDisponible = "No pudimos contactar al servicio. Intentá de nuevo en unos minutos.";
    private const string RutaSolicitudesAdmin = "v1/admin/solicitudes";

    public async Task<VerificacionResponse?> VerificarAsync(string cae, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            $"v1/comprobantes/verificar/{Uri.EscapeDataString(cae)}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<VerificacionResponse>(cancellationToken);
    }

    public async Task<SolicitudAltaResultado> EnviarSolicitudAltaAsync(
        SolicitudAltaRequest solicitud,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "v1/solicitudes-alta",
            solicitud,
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var recibida = await response.Content.ReadFromJsonAsync<SolicitudAltaResponse>(cancellationToken);
            return SolicitudAltaResultado.ConExito(recibida?.Mensaje ?? MensajeSolicitudRecibida);
        }

        var error = await LeerErrorAsync(response, cancellationToken);
        return error is null
            ? SolicitudAltaResultado.Fallido(null, MensajeErrorSolicitud)
            : SolicitudAltaResultado.Fallido(error.Codigo, error.Mensaje);
    }

    public Task<IReadOnlyList<string>?> LoginEmpresaAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
        => LoginAsync("v1/auth/login", email, password, cancellationToken);

    public Task<IReadOnlyList<string>?> LoginAdminGlobalAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
        => LoginAsync("v1/admin/auth/login", email, password, cancellationToken);

    public async Task<SesionEmpresaResponse?> GetMeEmpresaAsync(
        string? cookie,
        CancellationToken cancellationToken = default)
    {
        using var request = CrearRequest(HttpMethod.Get, "v1/auth/me", cookie);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SesionEmpresaResponse>(cancellationToken);
    }

    public async Task<SesionAdminResponse?> GetMeAdminAsync(
        string? cookie,
        CancellationToken cancellationToken = default)
    {
        using var request = CrearRequest(HttpMethod.Get, "v1/admin/auth/me", cookie);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SesionAdminResponse>(cancellationToken);
    }

    public Task<IReadOnlyList<string>> LogoutEmpresaAsync(
        string? cookie,
        CancellationToken cancellationToken = default)
        => LogoutAsync("v1/auth/logout", cookie, cancellationToken);

    public Task<IReadOnlyList<string>> LogoutAdminGlobalAsync(
        string? cookie,
        CancellationToken cancellationToken = default)
        => LogoutAsync("v1/admin/auth/logout", cookie, cancellationToken);

    public Task<ResultadoApi<PaginaResponse<SolicitudAltaResumenResponse>>> ListarSolicitudesAsync(
        string? cookie,
        int page,
        string? estado,
        string? cuit,
        string? razonSocial,
        string? email,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string> { $"page={page}", "pageSize=20" };
        AgregarFiltro(query, "estado", estado);
        AgregarFiltro(query, "cuit", cuit);
        AgregarFiltro(query, "razonSocial", razonSocial);
        AgregarFiltro(query, "email", email);

        return EnviarAsync<PaginaResponse<SolicitudAltaResumenResponse>>(
            HttpMethod.Get,
            $"{RutaSolicitudesAdmin}?{string.Join('&', query)}",
            cookie,
            null,
            cancellationToken);
    }

    public Task<ResultadoApi<SolicitudAltaDetalleResponse>> ObtenerSolicitudAsync(
        string? cookie,
        Guid id,
        CancellationToken cancellationToken = default)
        => EnviarAsync<SolicitudAltaDetalleResponse>(
            HttpMethod.Get,
            $"{RutaSolicitudesAdmin}/{id}",
            cookie,
            null,
            cancellationToken);

    public Task<ResultadoApi<SolicitudAltaDetalleResponse>> EditarSolicitudAsync(
        string? cookie,
        Guid id,
        EditarSolicitudRequest request,
        CancellationToken cancellationToken = default)
        => EnviarAsync<SolicitudAltaDetalleResponse>(
            HttpMethod.Put,
            $"{RutaSolicitudesAdmin}/{id}",
            cookie,
            request,
            cancellationToken);

    public Task<ResultadoApi<AprobarSolicitudResponse>> AprobarSolicitudAsync(
        string? cookie,
        Guid id,
        CancellationToken cancellationToken = default)
        => EnviarAsync<AprobarSolicitudResponse>(
            HttpMethod.Post,
            $"{RutaSolicitudesAdmin}/{id}/aprobar",
            cookie,
            null,
            cancellationToken);

    public Task<ResultadoApi<RechazarSolicitudResponse>> RechazarSolicitudAsync(
        string? cookie,
        Guid id,
        string motivo,
        CancellationToken cancellationToken = default)
        => EnviarAsync<RechazarSolicitudResponse>(
            HttpMethod.Post,
            $"{RutaSolicitudesAdmin}/{id}/rechazar",
            cookie,
            new RechazarSolicitudRequest(motivo),
            cancellationToken);

    public Task<ResultadoApi<ReenviarInvitacionResponse>> ReenviarInvitacionAsync(
        string? cookie,
        Guid id,
        CancellationToken cancellationToken = default)
        => EnviarAsync<ReenviarInvitacionResponse>(
            HttpMethod.Post,
            $"{RutaSolicitudesAdmin}/{id}/reenviar-invitacion",
            cookie,
            null,
            cancellationToken);

    public Task<ResultadoApi<InvitacionValidacionResponse>> ValidarInvitacionAsync(
        string token,
        CancellationToken cancellationToken = default)
        => EnviarAsync<InvitacionValidacionResponse>(
            HttpMethod.Get,
            $"v1/invitaciones/{Uri.EscapeDataString(token)}",
            null,
            null,
            cancellationToken);

    public Task<ResultadoApi<AceptarInvitacionResponse>> AceptarInvitacionAsync(
        string token,
        string password,
        string confirmacionPassword,
        CancellationToken cancellationToken = default)
        => EnviarAsync<AceptarInvitacionResponse>(
            HttpMethod.Post,
            $"v1/invitaciones/{Uri.EscapeDataString(token)}/aceptar",
            null,
            new AceptarInvitacionRequest(password, confirmacionPassword),
            cancellationToken);

    private async Task<ResultadoApi<T>> EnviarAsync<T>(
        HttpMethod method,
        string path,
        string? cookie,
        object? body,
        CancellationToken cancellationToken)
    {
        using var request = CrearRequest(method, path, cookie);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType());
        }

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return ResultadoApi<T>.Fallido(HttpStatusCode.ServiceUnavailable, null, MensajeApiNoDisponible);
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                var valor = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
                return valor is null
                    ? ResultadoApi<T>.Fallido(response.StatusCode, null, MensajeApiNoDisponible)
                    : ResultadoApi<T>.ConExito(valor);
            }

            var error = await LeerErrorAsync(response, cancellationToken);
            return ResultadoApi<T>.Fallido(
                response.StatusCode,
                error?.Codigo,
                error?.Mensaje ?? MensajeApiNoDisponible);
        }
    }

    private static void AgregarFiltro(List<string> query, string nombre, string? valor)
    {
        if (!string.IsNullOrWhiteSpace(valor))
        {
            query.Add($"{nombre}={Uri.EscapeDataString(valor.Trim())}");
        }
    }

    private async Task<IReadOnlyList<string>?> LoginAsync(
        string path,
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            path,
            new LoginRequest(email, password),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return LeerSetCookie(response);
    }

    private async Task<IReadOnlyList<string>> LogoutAsync(
        string path,
        string? cookie,
        CancellationToken cancellationToken)
    {
        using var request = CrearRequest(HttpMethod.Post, path, cookie);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        return LeerSetCookie(response);
    }

    private static HttpRequestMessage CrearRequest(HttpMethod method, string path, string? cookie)
    {
        var request = new HttpRequestMessage(method, path);
        if (!string.IsNullOrWhiteSpace(cookie))
        {
            request.Headers.TryAddWithoutValidation(CookieHeader, cookie);
        }

        return request;
    }

    private static async Task<ErrorResponse?> LeerErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(cancellationToken);
            return string.IsNullOrWhiteSpace(error?.Mensaje) ? null : error;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static IReadOnlyList<string> LeerSetCookie(HttpResponseMessage response)
        => response.Headers.TryGetValues(SetCookieHeader, out var values)
            ? values.ToArray()
            : [];
}