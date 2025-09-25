using Payroc.LoadBalancer;
using FluentAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Payroc.LoadBalancer.Tests;

public class Tests : IDisposable
{
    private const string LoadBalancerUri = "http://localhost:8080";
    
    private const string Target1Response = "Target 1";
    private const string Target2Response = "Target 2";

    private readonly WireMockServer _target1;
    private readonly WireMockServer _target2;

    public Tests()
    {
        _target1 = WireMockServer.Start(port: 11_000);
        
        _target1
            .Given(Request.Create())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("response-Type", "text/plain")
                .WithBody(Target1Response));

        _target2 = WireMockServer.Start(port: 11_001);
        
        _target2
            .Given(Request.Create())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("response-Type", "text/plain")
                .WithBody(Target2Response));

        _ = Task.Run(async () =>
        {
            var endpoints = "localhost:11000;localhost:11001";
            
            await Program.Main([endpoints]);
        });
    }

    [Fact]
    public async Task Should_ProxyConnection_ToTarget()
    {
        // Arrange
        
        // Act
        var response = await SendHttpMessage();
        
        // Assert
        response.Should().Be(Target1Response);
    }
    
    [Fact]
    public async Task Should_ProxySequentialConnections_ToTargets_UsingRoundRobinStrategy()
    {
        // Arrange
        
        // Act
        var response1 = await SendHttpMessage();
        var response2 = await SendHttpMessage();
        var response3 = await SendHttpMessage();

        // Assert
        response1.Should().Be(Target1Response);
        response2.Should().Be(Target2Response);
        response3.Should().Be(Target1Response);
    }

    [Fact]
    public async Task Should_SkipTarget_WhenCannotConnectToTarget()
    {
        // Act
        var response1 = await SendHttpMessage();
        var response2 = await SendHttpMessage();

        _target1.Stop();
        
        var response3 = await SendHttpMessage();
        
        // Assert
        response1.Should().Be(Target1Response);
        response2.Should().Be(Target2Response);
        response3.Should().Be(Target2Response);
    }

    private static async Task<string> SendHttpMessage()
    {
        // Use a new client for each request to simulate
        // multiple client connections
        var response = await new HttpClient().GetAsync(LoadBalancerUri);
        
        return await response.Content.ReadAsStringAsync();
    }

    public void Dispose()
    {
        _target1.Stop();
        _target2.Stop();
    }
}