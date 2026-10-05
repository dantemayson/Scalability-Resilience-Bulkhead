namespace SARDemo.Api.Services.Implementations;

public class FaultInjectingHandler : DelegatingHandler
{
    private readonly Random _random = new();
    private readonly ILogger<FaultInjectingHandler> _logger;

    public FaultInjectingHandler(ILogger<FaultInjectingHandler> logger)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (_random.Next(1, 10) <= 3)
        {
            _logger.LogWarning(
                "FaultInjectingHandler: Simulating temporary failure for {Url}",
                request.RequestUri);
            throw new HttpRequestException("The recommendation service is temporarily unavailable.");
        }

        return await base.SendAsync(request, cancellationToken);
    }
}