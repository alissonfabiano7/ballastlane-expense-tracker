using BallastLane.Api.Tests.TestHost;
using Shouldly;

namespace BallastLane.Api.Tests;

public class SecurityHeadersTests : IClassFixture<BallastLaneApiFactory>
{
    private readonly BallastLaneApiFactory _factory;

    public SecurityHeadersTests(BallastLaneApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Response_includes_required_security_headers_and_omits_server()
    {
        using HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/health");

        response.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");
        response.Headers.GetValues("X-Frame-Options").ShouldContain("DENY");
        response.Headers.GetValues("Referrer-Policy").ShouldContain("strict-origin-when-cross-origin");
        response.Headers.Contains("Server").ShouldBeFalse(
            "Server header must be removed to avoid stack fingerprinting.");
    }
}
