using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TickPulse.Api.Data;
using Xunit;

namespace TickPulse.Tests;

// Spins up the real API in memory, with Postgres swapped for an in-memory
// database and the background engine and analyst worker switched off.
// This proves the HTTP surface works end to end without any infra.
public sealed class QuotesApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public QuotesApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                var dbDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<TickPulseDbContext>));
                if (dbDescriptor is not null)
                {
                    services.Remove(dbDescriptor);
                }

                services.AddDbContext<TickPulseDbContext>(options =>
                    options.UseInMemoryDatabase("TickPulseTests"));

                foreach (var hosted in services.Where(d => d.ServiceType == typeof(IHostedService)).ToList())
                {
                    services.Remove(hosted);
                }
            });
        });
    }

    [Fact]
    public async Task SymbolsEndpointReturnsSeededSymbols()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/symbols");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("ACME", body);
        Assert.Contains("PULS", body);
    }

    [Fact]
    public async Task QuotesEndpointReturnsEmptyListBeforeAnyTicks()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/quotes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("[]", body.Trim());
    }

    [Fact]
    public async Task UnknownSymbolReturns404()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/symbols/NOPE");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AnalystStatusReportsRuleBasedWithoutApiKey()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/analyst/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("rule-based", body);
    }

    [Fact]
    public async Task WatchlistRoundTripWorks()
    {
        var client = _factory.CreateClient();

        var add = await client.PostAsJsonAsync("/api/watchlist", new { ticker = "ACME" });
        Assert.Equal(HttpStatusCode.Created, add.StatusCode);

        var list = await client.GetAsync("/api/watchlist");
        var body = await list.Content.ReadAsStringAsync();
        Assert.Contains("ACME", body);

        var remove = await client.DeleteAsync("/api/watchlist/ACME");
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);
    }
}
