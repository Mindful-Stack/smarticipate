using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace Smarticipate.Web.Authentication;

public class CookieHandler : DelegatingHandler
{ 
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"Sending request to: {request.RequestUri}");

        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        
        if (!request.Headers.Contains("X-Requested-With"))
        {
            request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        }
        
        return base.SendAsync(request, cancellationToken);
    }
}