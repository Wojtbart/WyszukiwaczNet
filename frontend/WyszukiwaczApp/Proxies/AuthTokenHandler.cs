using System.Net.Http.Headers;

namespace WyszukiwaczApp.Proxies;

public class AuthTokenHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(Globals.AuthToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Globals.AuthToken);

        return await base.SendAsync(request, cancellationToken);
    }
}
