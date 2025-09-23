using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace ActualGameSearch.ContractTests;

public class ApiTestFactory : WebApplicationFactory<Program>
{
	protected override IHost CreateHost(IHostBuilder builder)
	{
		builder.UseEnvironment("Test");
		// Ensure content root is the API project folder so static files and appsettings.Test.json resolve properly
		builder.ConfigureHostConfiguration(cfg => { });
		return base.CreateHost(builder);
	}
}
