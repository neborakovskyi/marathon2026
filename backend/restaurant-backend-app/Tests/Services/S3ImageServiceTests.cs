using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;
using APIHandler.Services;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Text;

namespace APIHandler.Tests.Services;

public class S3ImageServiceTests
{
    private static readonly byte[] PngBytes = CreateValidPng();

    private readonly Mock<IAmazonS3> _s3 = new();
    private readonly S3ImageService _service;

    public S3ImageServiceTests() => _service = new S3ImageService(_s3.Object);

    [Fact]
    public async Task UploadImageAsync_WithFolder_UploadsPngWithExpectedKeyAndContentType()
    {
        PutObjectRequest? captured = null;
        byte[]? uploadedBytes = null;
        _s3.Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((request, _) =>
            {
                captured = request;
                using var buffer = new MemoryStream();
                request.InputStream.CopyTo(buffer);
                uploadedBytes = buffer.ToArray();
            })
            .ReturnsAsync(new PutObjectResponse());

        var key = await _service.UploadImageAsync(PngBytes, "images", "dish-1.png",
            new Mock<ILambdaLogger>().Object, "dishes");

        Assert.Equal("dishes/dish-1.png", key);
        Assert.NotNull(captured);
        Assert.Equal("images", captured.BucketName);
        Assert.Equal(key, captured.Key);
        Assert.Equal("image/png", captured.ContentType);
        Assert.Equal(PngBytes, uploadedBytes);
    }

    [Fact]
    public async Task UploadImageAsync_WithoutFolder_UsesFileNameAsKey()
    {
        string? key = null;
        _s3.Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((request, _) => key = request.Key)
            .ReturnsAsync(new PutObjectResponse());

        var result = await _service.UploadImageAsync(PngBytes, "images", "photo.png",
            new Mock<ILambdaLogger>().Object, "  ");

        Assert.Equal("photo.png", result);
        Assert.Equal("photo.png", key);
    }

    [Fact]
    public async Task GetImageKey_DataUri_DecodesAndUploadsImage()
    {
        PutObjectRequest? captured = null;
        byte[]? uploadedBytes = null;
        _s3.Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((request, _) =>
            {
                captured = request;
                using var buffer = new MemoryStream();
                request.InputStream.CopyTo(buffer);
                uploadedBytes = buffer.ToArray();
            })
            .ReturnsAsync(new PutObjectResponse());
        var dataUri = $"data:image/png;base64,{Convert.ToBase64String(PngBytes)}";

        var key = await _service.GetImageKey(dataUri, "images", "assets", "photo",
            new Mock<ILambdaLogger>().Object);

        Assert.Equal("assets/photo", key);
        Assert.Equal(PngBytes, uploadedBytes);
    }

    [Fact]
    public async Task GetImageKey_InvalidBase64_ThrowsBeforeUploading()
    {
        await Assert.ThrowsAsync<FormatException>(() =>
            _service.GetImageKey("data:image/png;base64,invalid!", "images", "assets", "photo",
                new Mock<ILambdaLogger>().Object));

        _s3.Verify(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void GetImageBytesFromBase64_AcceptsRawBase64AndDataUri()
    {
        var encoded = Convert.ToBase64String(PngBytes);

        Assert.Equal(PngBytes, S3ImageService.GetImageBytesFromBase64(encoded));
        Assert.Equal(PngBytes, S3ImageService.GetImageBytesFromBase64($"data:image/png;base64,{encoded}"));
    }

    [Fact]
    public void GetPublicUrl_UsesConfiguredRegionAndImageKey()
    {
        var original = Environment.GetEnvironmentVariable("REGION");
        try
        {
            Environment.SetEnvironmentVariable("REGION", "us-west-2");

            var url = _service.GetPublicUrl("restaurant-images", "dishes/dish-1.png");

            Assert.Equal("https://restaurant-images.s3.us-west-2.amazonaws.com/dishes/dish-1.png", url);
        }
        finally
        {
            Environment.SetEnvironmentVariable("REGION", original);
        }
    }

    [Theory]
    [InlineData(false, 10)]
    [InlineData(true, 60)]
    public void PresignedImageUrlMethods_RequestExpectedBucketKeyAndExpiration(bool longLived, int lifetimeMinutes)
    {
        GetPreSignedUrlRequest? captured = null;
        _s3.Setup(s => s.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>()))
            .Callback<GetPreSignedUrlRequest>(request => captured = request)
            .Returns("https://signed.example/image");
        var before = DateTime.UtcNow;

        var url = longLived
            ? _service.GetPreSignedImageUrl("images", "dish.png")
            : _service.GetImageUrl("images", "dish.png");

        var after = DateTime.UtcNow;
        Assert.Equal("https://signed.example/image", url);
        Assert.NotNull(captured);
        Assert.Equal("images", captured.BucketName);
        Assert.Equal("dish.png", captured.Key);
        Assert.InRange(captured.Expires!.Value, before.AddMinutes(lifetimeMinutes), after.AddMinutes(lifetimeMinutes));
    }

    [Fact]
    public async Task DownloadImageAsync_ReturnsDownloadedBytes()
    {
        var payload = Encoding.UTF8.GetBytes("image-content");
        _s3.Setup(s => s.GetObjectAsync("images", "dishes/photo.png", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetObjectResponse { ResponseStream = new MemoryStream(payload) });

        var result = await _service.DownloadImageAsync("images", "dishes/photo.png");

        Assert.Equal(payload, result);
    }

    [Fact]
    public async Task DeleteImageAsync_DeletesRequestedObject()
    {
        _s3.Setup(s => s.DeleteObjectAsync("images", "dishes/photo.png", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteObjectResponse());

        await _service.DeleteImageAsync("images", "dishes/photo.png");

        _s3.Verify(s => s.DeleteObjectAsync("images", "dishes/photo.png", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void CompareImagesHash_ReturnsTrueOnlyForIdenticalContent()
    {
        Assert.True(S3ImageService.CompareImagesHash([1, 2, 3], [1, 2, 3]));
        Assert.False(S3ImageService.CompareImagesHash([1, 2, 3], [1, 2, 4]));
    }

    [Fact]
    public async Task UploadImageAsync_InvalidImageBytes_ThrowsAndDoesNotCallS3()
    {
        await Assert.ThrowsAnyAsync<Exception>(() =>
            _service.UploadImageAsync([0, 1, 2, 3], "images", "not-image",
                new Mock<ILambdaLogger>().Object));

        _s3.Verify(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static byte[] CreateValidPng()
    {
        using var image = Image.LoadPixelData<Rgba32>([new Rgba32(255, 0, 0, 255)], 1, 1);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }
}
