namespace LoginShot.Storage;

public interface ICaptureStorageService
{
    Task<CapturePersistenceResult> PersistAsync(CapturePersistenceRequest request, CancellationToken cancellationToken = default);
}
