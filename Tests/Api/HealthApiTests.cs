using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Tests.Api;

public class HealthApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
	[Fact]
	public async Task Health_WhenDatabaseIsUnreachable_Returns503()
	{
		var client = factory
			.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
				config.AddInMemoryCollection(new Dictionary<string, string?>
				{
					// Nothing listens on port 1, so the connection is refused immediately.
					["ConnectionStrings:Default"] = "Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x;Timeout=2"
				})))
			.CreateClient();

		var response = await client.GetAsync("/health");
		var body = await response.Content.ReadAsStringAsync();

		Assert.True(response.StatusCode == HttpStatusCode.ServiceUnavailable,
			$"{(int)response.StatusCode}: {body}");
	}
}
