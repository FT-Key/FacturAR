using System.Net;
using System.Net.Http.Json;
using FacturAR.Contracts.Auth;
using FacturAR.Contracts.Comprobantes;

namespace FacturAR.Web.Services;

public sealed class FacturarApiClient(HttpClient httpClient)
{
    private const string CookieHeader = "Cookie";
    private const string SetCookieHeader = "Set-Cookie";

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

    private static IReadOnlyList<string> LeerSetCookie(HttpResponseMessage response)
        => response.Headers.TryGetValues(SetCookieHeader, out var values)
            ? values.ToArray()
            : [];
}