using System.Text.Json;
using LoginShot.Triggers;

namespace LoginShot.Storage;

public sealed class CaptureStorageService : ICaptureStorageService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly IAtomicFileWriter fileWriter;

    public CaptureStorageService(IAtomicFileWriter fileWriter)
    {
        this.fileWriter = fileWriter;
    }

    public Task<CapturePersistenceResult> PersistAsync(CapturePersistenceRequest request, CancellationToken cancellationToken = default)
    {
        fileWriter.EnsureDirectory(request.OutputDirectory);

        var imageFileName = CaptureFileNameBuilder.Build(request.TimestampUtc, request.EventType, request.Extension);
        var imagePath = Path.Combine(request.OutputDirectory, imageFileName);
        var sidecarPath = Path.ChangeExtension(imagePath, ".json")
            ?? throw new InvalidOperationException("Unable to build sidecar path.");

        var isSuccess = request.Failure is null && request.ImageBytes is { Length: > 0 };
        if (isSuccess)
        {
            fileWriter.WriteAllBytesAtomic(imagePath, request.ImageBytes!);
        }

        if (request.WriteSidecar)
        {
            var sidecar = BuildSidecar(request, isSuccess ? imagePath : null);
            var sidecarJson = JsonSerializer.Serialize(sidecar, JsonOptions);
            fileWriter.WriteAllTextAtomic(sidecarPath, sidecarJson);
        }

        return Task.FromResult(new CapturePersistenceResult(
            ImageWritten: isSuccess,
            ImagePath: isSuccess ? imagePath : null,
            SidecarPath: request.WriteSidecar ? sidecarPath : null));
    }

    private static CaptureSidecar BuildSidecar(CapturePersistenceRequest request, string? imagePath)
    {
        return new CaptureSidecar(
            request.TimestampUtc,
            ToEventTag(request.EventType),
            request.Hostname,
            request.Username,
            imagePath,
            imagePath is null ? "failure" : "success",
            request.Failure,
            request.App,
            request.Camera);
    }

    private static string ToEventTag(SessionEventType eventType)
    {
        return eventType.ToString().ToLowerInvariant();
    }

    private sealed record CaptureSidecar(
        DateTimeOffset Timestamp,
        string Event,
        string Hostname,
        string Username,
        string? OutputPath,
        string Status,
        CaptureFailureInfo? Failure,
        CaptureAppInfo App,
        CaptureCameraInfo Camera);
}
