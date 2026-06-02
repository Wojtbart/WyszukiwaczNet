using System.Net.Http.Headers;
using WyszukiwaczApp.Services;

namespace WyszukiwaczApp.Proxies;

public class AuthTokenHandler : DelegatingHandler
{
    private readonly AuthState _authState;

    public AuthTokenHandler(AuthState authState)
    {
        _authState = authState;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_authState.AuthToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authState.AuthToken);

        return await base.SendAsync(request, cancellationToken);
    }
}
