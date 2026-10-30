using System.Net;
using FacturAR.Api.Security;
using Microsoft.AspNetCore.Http;

namespace FacturAR.IntegrationTests;

public sealed class IpClienteTests
{
    [Fact]
    public void Obtener_ConfiaEnProxy_TomaLaUltimaIpDeXForwardedFor()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");
        context.Request.Headers["X-Forwarded-For"] = "1.1.1.1, 2.2.2.2";

        var ip = IpCliente.Obtener(context, confiarEnProxy: true);

        Assert.Equal("2.2.2.2", ip);
    }

    [Fact]
    public void Obtener_NoConfiaEnProxy_UsaLaIpRemota()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");
        context.Request.Headers["X-Forwarded-For"] = "1.1.1.1, 2.2.2.2";

        var ip = IpCliente.Obtener(context, confiarEnProxy: false);

        Assert.Equal("10.0.0.1", ip);
    }

    [Fact]
    public void Obtener_ConfiaEnProxyConHeaderVacio_UsaLaIpRemota()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");
        context.Request.Headers["X-Forwarded-For"] = string.Empty;

        var ip = IpCliente.Obtener(context, confiarEnProxy: true);

        Assert.Equal("10.0.0.1", ip);
    }

    [Fact]
    public void Obtener_SinIpRemotaNiHeader_DevuelveDesconocida()
    {
        var context = new DefaultHttpContext();

        var ip = IpCliente.Obtener(context, confiarEnProxy: false);

        Assert.Equal("desconocida", ip);
    }
}
