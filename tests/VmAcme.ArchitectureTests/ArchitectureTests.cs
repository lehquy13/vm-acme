using System.Reflection;
using System.Xml.Linq;
using MediatR;
using Xunit;

namespace VmAcme;

public sealed class ArchitectureTests
{
    private static readonly Assembly DomainAssembly = Assembly.Load("VmAcme.Domain");
    private static readonly Assembly ApplicationAssembly = Assembly.Load("VmAcme.Application");
    private static readonly Assembly InfrastructureAssembly = Assembly.Load("VmAcme.Infrastructure");
    private static readonly Assembly WebApiAssembly = Assembly.Load("VmAcme.WebApi");

    [Fact]
    public void Domain_has_no_outward_layer_or_framework_dependencies()
    {
        AssertNoAssemblyReferences(DomainAssembly, ["VmAcme.Application", "VmAcme.Infrastructure", "VmAcme.WebApi"]);
        AssertNoAssemblyReferences(DomainAssembly, IsForbiddenDomainAssembly);
        AssertNoProjectReferences("VmAcme.Domain", ["VmAcme.Application", "VmAcme.Infrastructure", "VmAcme.WebApi"]);
        AssertNoFrameworkReferences("VmAcme.Domain");
        AssertNoPackageReferences("VmAcme.Domain", _ => true);
    }

    [Fact]
    public void Application_has_no_infrastructure_web_ef_or_aspnet_dependencies()
    {
        AssertNoAssemblyReferences(ApplicationAssembly, ["VmAcme.Infrastructure", "VmAcme.WebApi"]);
        AssertNoAssemblyReferences(ApplicationAssembly, IsForbiddenApplicationFrameworkAssembly);
        AssertNoProjectReferences("VmAcme.Application", ["VmAcme.Infrastructure", "VmAcme.WebApi"]);
        AssertNoFrameworkReferences("VmAcme.Application");
        AssertNoPackageReferences("VmAcme.Application", IsForbiddenApplicationFrameworkPackage);
    }

    [Fact]
    public void Infrastructure_has_no_webapi_or_fastendpoints_dependencies()
    {
        AssertNoAssemblyReferences(InfrastructureAssembly, ["VmAcme.WebApi", "FastEndpoints"]);
        AssertNoProjectReferences("VmAcme.Infrastructure", ["VmAcme.WebApi"]);
        AssertNoPackageReferences("VmAcme.Infrastructure",
            name => name.StartsWith("FastEndpoints", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Todo_and_user_endpoints_derive_from_fastendpoints_and_do_not_inject_repositories()
    {
        var endpoints = PublicTypes(WebApiAssembly)
            .Where(type => type.Namespace is "VmAcme.Endpoints.Todos" or "VmAcme.Endpoints.Users")
            .Where(type => type.Name.EndsWith("Endpoint", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(endpoints);
        foreach (var endpoint in endpoints)
        {
            Assert.True(HasFastEndpointsBase(endpoint),
                $"{endpoint.FullName} must derive from a FastEndpoints endpoint type.");

            var constructors =
                endpoint.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var parameter in constructors.SelectMany(constructor => constructor.GetParameters()))
            {
                var type = parameter.ParameterType;
                Assert.False(IsRepositoryType(type) || IsDbContextType(type)
                                                    || type.Namespace?.StartsWith("VmAcme.Infrastructure",
                                                        StringComparison.Ordinal) == true,
                    $"{endpoint.FullName} must not inject database repository {type.FullName}.");
            }
        }
    }

    [Fact]
    public void Every_application_mediatr_request_returns_a_result()
    {
        var requests = ApplicationAssembly.GetTypes()
            .SelectMany(type => type.GetInterfaces()
                .Where(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IRequest<>))
                .Select(contract => (Type: type, Response: contract.GetGenericArguments()[0])))
            .ToArray();

        Assert.NotEmpty(requests);
        foreach (var request in requests)
        {
            var response = request.Response;
            Assert.True(response.IsGenericType && response.GetGenericTypeDefinition().Name == "Result`1"
                                               && response.GetGenericTypeDefinition().Namespace == "VmAcme.Common",
                $"{request.Type.FullName} returns {response.FullName}; application requests must return Result<T>.");
        }
    }

    private static void AssertNoAssemblyReferences(Assembly assembly, IEnumerable<string> forbiddenNames) =>
        AssertNoAssemblyReferences(assembly, name => forbiddenNames.Contains(name, StringComparer.OrdinalIgnoreCase));

    private static void AssertNoAssemblyReferences(Assembly assembly, Func<string, bool> isForbidden)
    {
        var forbidden = assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(isForbidden)
            .ToArray();
        Assert.Empty(forbidden);
    }

    private static bool IsForbiddenApplicationFrameworkAssembly(string name) =>
        name.StartsWith("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("FastEndpoints", StringComparison.OrdinalIgnoreCase);

    private static bool IsForbiddenDomainAssembly(string name) =>
        name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("FastEndpoints", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("MediatR", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("FluentValidation", StringComparison.OrdinalIgnoreCase);

    private static bool IsForbiddenApplicationFrameworkPackage(string name) =>
        IsForbiddenApplicationFrameworkAssembly(name);

    private static bool IsRepositoryType(Type type) =>
        type.Name.EndsWith("Repository", StringComparison.Ordinal)
        || (type.IsGenericType && type.GetGenericArguments().Any(IsRepositoryType));

    private static bool IsDbContextType(Type type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.FullName == "Microsoft.EntityFrameworkCore.DbContext")
                return true;
        }

        return false;
    }

    private static bool HasFastEndpointsBase(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.Namespace == "FastEndpoints" && current.Name.StartsWith("Endpoint", StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static IEnumerable<Type> PublicTypes(Assembly assembly) =>
        assembly.GetTypes().Where(type => type.IsPublic || type.IsNestedPublic);

    private static void AssertNoProjectReferences(string projectName, IEnumerable<string> forbiddenNames)
    {
        var forbidden = forbiddenNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var includes = ProjectXml(projectName).Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(include => include is not null)
            .Select(include => Path.GetFileNameWithoutExtension(include!.Replace('\\', '/')))
            .Where(forbidden.Contains)
            .ToArray();

        Assert.Empty(includes);
    }

    private static void AssertNoFrameworkReferences(string projectName)
    {
        var includes = ProjectXml(projectName).Descendants()
            .Where(element => element.Name.LocalName == "FrameworkReference")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(include => include is not null)
            .ToArray();

        Assert.Empty(includes);
    }

    private static void AssertNoPackageReferences(string projectName, Func<string, bool> isForbidden)
    {
        var includes = ProjectXml(projectName).Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(include => include is not null && isForbidden(include))
            .ToArray();

        Assert.Empty(includes);
    }

    private static XDocument ProjectXml(string projectName) =>
        XDocument.Load(Path.Combine(FindRepositoryRoot(), "src", projectName, $"{projectName}.csproj"));

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "vm-acme.slnx")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not find vm-acme.slnx above the test output directory.");
    }
}