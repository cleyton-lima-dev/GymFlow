using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GymFlow.Application.Interfaces.Security;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace GymFlow.IntegrationTests.Security;

public partial class TenantEnrollmentAccessTests
{
    private static readonly string[] ReportKinds = ["students", "financial", "enrollments", "attendance", "access-events"];

    [Theory]
    [InlineData("anonymous", 401)]
    [InlineData("Student", 403)]
    [InlineData("Professor", 403)]
    [InlineData("AccessAgent", 403)]
    [InlineData("inactive", 401)]
    public async Task ReportsAndExports_EnforceRealJwtAuthorization(string identity, int status)
    {
        await using var factory = new TestApi();
        using var client = factory.CreateClient();
        if (identity == "AccessAgent")
        {
            var agent = new AccessAgent { Id = Guid.NewGuid(), GymId = factory.LegacyGym, Name = "Synthetic", MachineName = "isolated", SecretHash = "unused" };
            factory.WithDb(db => db.AccessAgents.Add(agent));
            using var scope = factory.Services.CreateScope();
            client.DefaultRequestHeaders.Authorization = new("Bearer", scope.ServiceProvider.GetRequiredService<IAccessAgentTokenService>().GenerateToken(agent));
        }
        else if (identity != "anonymous")
        {
            var user = factory.AddUser(factory.LegacyGym,
                identity == "Professor" ? UserRole.Professor : UserRole.Student, active: identity != "inactive");
            client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(user));
        }
        foreach (var kind in ReportKinds)
            foreach (var suffix in new[] { "", "/export" })
                Assert.Equal((HttpStatusCode)status, (await client.GetAsync($"/api/reports/{kind}{suffix}")).StatusCode);
    }

    [Fact]
    public Task ReportsHttp_AdminFiltersAndCsvUseAuthenticatedTenant() => VerifyReportsHttp(false);

    [PostgresTheory]
    [InlineData(true)]
    public Task PostgreSql_ReportsHttp_AdminFiltersAndCsvUseAuthenticatedTenant(bool postgres) => VerifyReportsHttp(postgres);

    private static async Task VerifyReportsHttp(bool postgres)
    {
        await using var factory = new TestApi(postgres);
        var admin = factory.AddUser(factory.LegacyGym, UserRole.Admin);
        var own = factory.AddUser(factory.LegacyGym);
        var foreign = factory.AddUser(factory.EnabledGym);
        Guid foreignId = default;
        factory.WithDb(db =>
        {
            db.Users.Single(u => u.Id == own.Id).Name = "Relatório sintético";
            db.Users.Single(u => u.Id == foreign.Id).Name = "Outro tenant não exportar";
            foreignId = db.Students.Single(s => s.UserId == foreign.Id).Id;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        foreach (var kind in ReportKinds)
        {
            var response = await client.GetAsync($"/api/reports/{kind}?gymId={factory.EnabledGym}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl!.NoStore);
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(kind == "students" ? 1 : 0, result.GetProperty("totalCount").GetInt32());
            var export = await client.GetAsync($"/api/reports/{kind}/export?gymId={factory.EnabledGym}");
            Assert.Equal(HttpStatusCode.OK, export.StatusCode);
            Assert.Equal("text/csv", export.Content.Headers.ContentType!.MediaType);
            var bytes = await export.Content.ReadAsByteArrayAsync();
            Assert.Equal(new byte[] { 0xef, 0xbb, 0xbf }, bytes.Take(3));
            Assert.DoesNotContain("Outro tenant", Encoding.UTF8.GetString(bytes));
            var filtered = await client.GetFromJsonAsync<JsonElement>($"/api/reports/{kind}?studentId={foreignId}");
            Assert.Equal(0, filtered.GetProperty("totalCount").GetInt32());
        }
        var searched = await client.GetFromJsonAsync<JsonElement>("/api/reports/students?search=Relat%C3%B3rio&status=active&enrollmentStatus=none");
        Assert.Equal(1, searched.GetProperty("totalCount").GetInt32());
        foreach (var query in new[] { "page=0", "pageSize=101", "status=invalid", "studentId=invalid", "fromDate=invalid", "view=invalid" })
            foreach (var suffix in new[] { "", "/export" })
                Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/reports/financial{suffix}?{query}")).StatusCode);
    }
}
