using System.Net;
using BallastLane.Api.Tests.TestHost;
using Shouldly;

namespace BallastLane.Api.Tests;

public class HealthCheckTests : IClassFixture<BallastLaneApiFactory>
{
    private readonly BallastLaneApiFactory _factory;

    public HealthCheckTests(BallastLaneApiFactory factory) => _factory = factory;

    [Fact]
    public async Task GET_health_returns_200_with_status_Healthy()
    {
        using HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        string body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("Healthy");
    }

    [Fact]
    public async Task GET_health_ready_returns_200_when_db_is_up()
    {
        using HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/health/ready");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        string body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("Healthy");
    }
}
