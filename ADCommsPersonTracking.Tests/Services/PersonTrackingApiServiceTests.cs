using System.Net;
using System.Text;
using ADCommsPersonTracking.Web.Models;
using ADCommsPersonTracking.Web.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace ADCommsPersonTracking.Tests.Services;

public class PersonTrackingApiServiceTests
{
    private readonly Mock<ILogger<PersonTrackingApiService>> _loggerMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly PersonTrackingApiService _service;

    public PersonTrackingApiServiceTests()
    {
        _loggerMock = new Mock<ILogger<PersonTrackingApiService>>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost")
        };
        _service = new PersonTrackingApiService(_httpClient, _loggerMock.Object);
    }

    [Fact]
    public async Task UploadVideoAsync_WithSeekableStream_SucceedsOnFirstAttempt()
    {
        // Arrange
        var fileName = "test.mp4";
        var videoData = Encoding.UTF8.GetBytes("fake video content");
        var videoStream = new MemoryStream(videoData);
        
        var expectedResponse = new VideoUploadResponse
        {
            TrackingId = "test-tracking-id",
            FrameCount = 100,
            WasCached = false,
            Message = "Upload successful"
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(expectedResponse),
                    Encoding.UTF8,
                    "application/json")
            });

        // Act
        var result = await _service.UploadVideoAsync(videoStream, fileName);

        // Assert
        result.Should().NotBeNull();
        result!.TrackingId.Should().Be("test-tracking-id");
        result.FrameCount.Should().Be(100);
    }

    [Fact]
    public async Task UploadVideoAsync_WithSeekableStream_HandlesStreamReset()
    {
        // Arrange
        var fileName = "test.mp4";
        var videoData = Encoding.UTF8.GetBytes("fake video content");
        
        var expectedResponse = new VideoUploadResponse
        {
            TrackingId = "test-tracking-id",
            FrameCount = 100,
            WasCached = false,
            Message = "Upload successful"
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(expectedResponse),
                    Encoding.UTF8,
                    "application/json")
            });

        // Act - simulate what happens during a retry scenario
        // The stream position should be reset to 0 before upload
        var videoStream = new MemoryStream(videoData);
        videoStream.Position = videoData.Length; // Move to end to simulate a "consumed" stream
        
        var result = await _service.UploadVideoAsync(videoStream, fileName);

        // Assert
        // The method should still succeed because it resets the position to 0
        result.Should().NotBeNull();
        result!.TrackingId.Should().Be("test-tracking-id");
        result.FrameCount.Should().Be(100);
    }

    [Fact]
    public async Task UploadVideoAsync_WithNonSeekableStream_BuffersAndSucceeds()
    {
        // Arrange
        var fileName = "test.mp4";
        var videoData = Encoding.UTF8.GetBytes("fake video content");
        var nonSeekableStream = new NonSeekableMemoryStream(videoData);
        
        var expectedResponse = new VideoUploadResponse
        {
            TrackingId = "test-tracking-id",
            FrameCount = 100,
            WasCached = false,
            Message = "Upload successful"
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(expectedResponse),
                    Encoding.UTF8,
                    "application/json")
            });

        // Act
        var result = await _service.UploadVideoAsync(nonSeekableStream, fileName);

        // Assert
        result.Should().NotBeNull();
        result!.TrackingId.Should().Be("test-tracking-id");
        result.FrameCount.Should().Be(100);
    }

    [Fact]
    public async Task UploadVideoAsync_WithHttpError_ReturnsNull()
    {
        // Arrange
        var fileName = "test.mp4";
        var videoData = Encoding.UTF8.GetBytes("fake video content");
        var videoStream = new MemoryStream(videoData);

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent("Server error")
            });

        // Act
        var result = await _service.UploadVideoAsync(videoStream, fileName);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// Helper class to simulate a non-seekable stream
    /// </summary>
    private class NonSeekableMemoryStream : Stream
    {
        private readonly MemoryStream _innerStream;

        public NonSeekableMemoryStream(byte[] data)
        {
            _innerStream = new MemoryStream(data);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _innerStream.Length;
        public override long Position
        {
            get => _innerStream.Position;
            set => throw new NotSupportedException();
        }

        public override void Flush() => _innerStream.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _innerStream.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _innerStream.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
