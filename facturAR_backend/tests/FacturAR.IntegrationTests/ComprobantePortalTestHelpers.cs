using System.Net;
using System.Net.Http.Json;
using FacturAR.Contracts.Auth;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FacturAR.IntegrationTests;

internal static class ComprobantePortalTestHelpers
{
    public static HttpClient CrearCliente(ComprobanteApiKeyFactory factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            AllowAutoRedirect = false
        });

    public static async Task<string> IniciarSesionAsync(
        HttpClient client,
        string email = ComprobanteApiKeyFactory.EmailUsuario)
    {
        var login = await client.PostAsJsonAsync(
            "/v1/auth/login",
            new LoginRequest(email, ComprobanteApiKeyFactory.PasswordUsuario));

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return login.Headers.GetValues("Set-Cookie").First().Split(';')[0];
    }

    public static Task<HttpResponseMessage> EnviarConCookieAsync(
        HttpClient client,
        string path,
        object body,
        string cookie,
        string? idempotencyKey = null)
    {
        var mensaje = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body)
        };
        mensaje.Headers.TryAddWithoutValidation("Cookie", cookie);

        if (idempotencyKey is not null)
        {
            mensaje.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        }

        return client.SendAsync(mensaje);
    }

    public static Task<HttpResponseMessage> EnviarSinSesionAsync(HttpClient client, string path, object body)
    {
        var mensaje = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body)
        };

        return client.SendAsync(mensaje);
    }

    public static Task<HttpResponseMessage> ObtenerConCookieAsync(
        HttpClient client,
        string path,
        string cookie)
    {
        var mensaje = new HttpRequestMessage(HttpMethod.Get, path);
        mensaje.Headers.TryAddWithoutValidation("Cookie", cookie);

        return client.SendAsync(mensaje);
    }
}
