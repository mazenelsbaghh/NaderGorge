using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NaderGorge.API.Controllers;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Data;
using Xunit;

namespace NaderGorge.Application.Tests;

public class SecureDownloadTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly FakeAccessCheckService _accessService;
    private readonly IConfiguration _config;
    private readonly FakeWebHostEnvironment _env;
    private readonly FakeMediator _mediator;
    private readonly string _testWwwRoot;

    public SecureDownloadTests()
    {
        _db = TestAppDbContextFactory.Create();
        _accessService = new FakeAccessCheckService();
        
        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Secret"] = "SuperSecretKeyForSignedDownloadTestingOnly!"
            })
            .Build();

        _testWwwRoot = Path.Combine(Directory.GetCurrentDirectory(), "TestWwwRoot_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testWwwRoot);

        _env = new FakeWebHostEnvironment { WebRootPath = _testWwwRoot };
        _mediator = new FakeMediator();
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_testWwwRoot))
        {
            Directory.Delete(_testWwwRoot, true);
        }
    }

    private (ContentController, PublicController) CreateControllers(Guid userId)
    {
        var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        var controllerContext = new ControllerContext { HttpContext = httpContext };

        var contentController = new ContentController(_mediator, _db, _accessService, _config)
        {
            ControllerContext = controllerContext
        };

        var publicController = new PublicController(_mediator, _db, _config, _env)
        {
            ControllerContext = controllerContext
        };

        return (contentController, publicController);
    }

    [Fact]
    public async Task SignDownload_ShouldReturnDownloadUrl_WhenAccessGranted()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var lessonId = Guid.NewGuid();
        var resource = new LessonResource
        {
            Id = Guid.NewGuid(),
            Title = "Test Document",
            FileUrl = "uploads/lessons/test.pdf",
            ResourceType = "PDF",
            LessonId = lessonId
        };
        _db.LessonResources.Add(resource);
        await _db.SaveChangesAsync();

        var (contentController, _) = CreateControllers(userId);
        _accessService.HasAccessResult = true;

        // Act
        var result = await contentController.SignDownload(resource.Id, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = okResult.Value;
        Assert.NotNull(value);
        
        var successProp = value.GetType().GetProperty("Success")?.GetValue(value);
        var urlProp = value.GetType().GetProperty("DownloadUrl")?.GetValue(value);
        
        Assert.NotNull(successProp);
        Assert.NotNull(urlProp);

        Assert.True((bool)successProp);
        Assert.Contains($"/api/public/resources/{resource.Id}/download?token=", (string)urlProp);
    }

    [Fact]
    public async Task SignDownload_ShouldReturn403_WhenAccessDenied()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var lessonId = Guid.NewGuid();
        var resource = new LessonResource
        {
            Id = Guid.NewGuid(),
            Title = "Secret Document",
            FileUrl = "uploads/lessons/secret.pdf",
            ResourceType = "PDF",
            LessonId = lessonId
        };
        _db.LessonResources.Add(resource);
        await _db.SaveChangesAsync();

        var (contentController, _) = CreateControllers(userId);
        _accessService.HasAccessResult = false;

        // Act
        var result = await contentController.SignDownload(resource.Id, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objectResult.StatusCode);
    }

    [Fact]
    public async Task DownloadResource_ShouldReturnFile_WhenTokenIsValid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var lessonId = Guid.NewGuid();
        var resource = new LessonResource
        {
            Id = Guid.NewGuid(),
            Title = "Valid Document",
            FileUrl = "uploads/lessons/valid.pdf",
            ResourceType = "PDF",
            LessonId = lessonId
        };
        _db.LessonResources.Add(resource);
        await _db.SaveChangesAsync();

        // Create the physical test file
        var uploadDir = Path.Combine(_testWwwRoot, "uploads", "lessons");
        Directory.CreateDirectory(uploadDir);
        var filePath = Path.Combine(uploadDir, "valid.pdf");
        await File.WriteAllTextAsync(filePath, "PDF Dummy Content");

        var (contentController, publicController) = CreateControllers(userId);

        // Act: Sign the URL
        var signResult = await contentController.SignDownload(resource.Id, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(signResult);
        var value = okResult.Value;
        Assert.NotNull(value);
        
        var downloadUrlProp = value.GetType().GetProperty("DownloadUrl")?.GetValue(value);
        Assert.NotNull(downloadUrlProp);
        var downloadUrl = (string)downloadUrlProp;

        // Extract and decode token (since URL is escaped)
        var queryIndex = downloadUrl.IndexOf("?token=");
        var token = Uri.UnescapeDataString(downloadUrl.Substring(queryIndex + "?token=".Length));

        // Act: Download the file
        var downloadResult = await publicController.DownloadResource(resource.Id, token, CancellationToken.None);

        // Assert
        var fileResult = Assert.IsType<PhysicalFileResult>(downloadResult);
        Assert.Equal(filePath, fileResult.FileName);
        Assert.Equal("application/octet-stream", fileResult.ContentType);
    }

    [Fact]
    public async Task DownloadResource_ShouldReturnBadRequest_WhenTokenIsExpired()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var (contentController, publicController) = CreateControllers(userId);

        // Generate an expired token (expiry 5 minutes in past)
        var expiresUnixSeconds = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds();
        var payload = $"{userId}:{expiresUnixSeconds}";
        
        var secret = "SuperSecretKeyForSignedDownloadTestingOnly!";
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(secret);
        using var hmac = new System.Security.Cryptography.HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes($"{resourceId}:{payload}"));
        var signature = Convert.ToHexString(hashBytes);
        var token = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{payload}:{signature}"));

        // Act
        var result = await publicController.DownloadResource(resourceId, token, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var msgProp = badRequestResult.Value?.GetType().GetProperty("Message")?.GetValue(badRequestResult.Value);
        Assert.Equal("Token has expired.", msgProp);
    }

    [Fact]
    public async Task DownloadResource_ShouldPreventPathTraversal_WhenRelativePathGoesOutsideRoot()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var lessonId = Guid.NewGuid();
        
        // Evil path attempting traversal
        var resource = new LessonResource
        {
            Id = Guid.NewGuid(),
            Title = "Evil Document",
            FileUrl = "uploads/lessons/../../secret.txt",
            ResourceType = "TXT",
            LessonId = lessonId
        };
        _db.LessonResources.Add(resource);
        await _db.SaveChangesAsync();

        // Create the outside secret file mock path (so that physically if checked it might pass, but traversal check must block it)
        var outsideSecretPath = Path.GetFullPath(Path.Combine(_testWwwRoot, "..", "secret.txt"));
        // Create test folder context for safety
        
        var (contentController, publicController) = CreateControllers(userId);
        
        // Act: Sign URL
        var signResult = await contentController.SignDownload(resource.Id, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(signResult);
        var value = okResult.Value;
        Assert.NotNull(value);

        var downloadUrlProp = value.GetType().GetProperty("DownloadUrl")?.GetValue(value);
        Assert.NotNull(downloadUrlProp);
        var downloadUrl = (string)downloadUrlProp;

        // Extract and decode token (since URL is escaped)
        var queryIndex = downloadUrl.IndexOf("?token=");
        var token = Uri.UnescapeDataString(downloadUrl.Substring(queryIndex + "?token=".Length));

        // Act: Try to download
        var downloadResult = await publicController.DownloadResource(resource.Id, token, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(downloadResult);
        var msgProp = badRequestResult.Value?.GetType().GetProperty("Message")?.GetValue(badRequestResult.Value);
        Assert.Equal("Invalid resource path.", msgProp);
    }

    // Fakes
    private class FakeAccessCheckService : IAccessCheckService
    {
        public bool HasAccessResult { get; set; } = true;
        public Task<bool> HasAccessToPackageAsync(Guid userId, Guid packageId, CancellationToken ct = default) => Task.FromResult(HasAccessResult);
        public Task<bool> HasAccessToLessonAsync(Guid userId, Guid lessonId, CancellationToken ct = default) => Task.FromResult(HasAccessResult);
        public Task<bool> HasAccessToExamAsync(Guid userId, Guid examId, CancellationToken ct = default) => Task.FromResult(HasAccessResult);
    }

    private class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = "";
        public string ContentRootPath { get; set; } = "";
        public string ApplicationName { get; set; } = "TestApplication";
        public string EnvironmentName { get; set; } = "Development";
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private class FakeMediator : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotImplementedException();
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
    }
}
