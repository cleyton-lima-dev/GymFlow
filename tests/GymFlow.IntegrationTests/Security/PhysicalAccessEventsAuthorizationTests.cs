using System.Reflection;
using GymFlow.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymFlow.IntegrationTests.Security;

public class PhysicalAccessEventsAuthorizationTests
{
    [Fact]
    public void GetEvents_ShouldRequireAdminRole()
    {
        var controllerType = typeof(PhysicalAccessController);

        var method = controllerType.GetMethod(
            nameof(PhysicalAccessController.GetEvents));

        Assert.NotNull(method);

        var controllerAuthorization = controllerType
            .GetCustomAttributes<AuthorizeAttribute>(true)
            .ToList();

        var actionAuthorization = method!
            .GetCustomAttributes<AuthorizeAttribute>(true)
            .ToList();

        var authorization = controllerAuthorization
            .Concat(actionAuthorization)
            .ToList();

        Assert.NotEmpty(authorization);

        Assert.Contains(
            authorization,
            attribute => attribute.Roles?
                .Split(',', StringSplitOptions.TrimEntries)
                .Contains("Admin") == true);

        Assert.DoesNotContain(
            controllerType.GetCustomAttributes<AllowAnonymousAttribute>(true),
            _ => true);

        Assert.DoesNotContain(
            method.GetCustomAttributes<AllowAnonymousAttribute>(true),
            _ => true);

        var httpGet = method.GetCustomAttribute<HttpGetAttribute>();

        Assert.NotNull(httpGet);
        Assert.Equal("events", httpGet!.Template);
    }
}
