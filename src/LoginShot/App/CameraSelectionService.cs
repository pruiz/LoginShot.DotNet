using LoginShot.Config;
using Microsoft.Extensions.Logging;

namespace LoginShot.App;

internal sealed class CameraSelectionService
{
    private readonly IConfigWriter configWriter;
    private readonly ILogger logger;

    public CameraSelectionService(IConfigWriter configWriter, ILogger logger)
    {
        this.configWriter = configWriter;
        this.logger = logger;
    }

    public bool TryApplySelection(LoginShotConfig currentConfig, int? cameraIndex, out LoginShotConfig updatedConfig, out string? errorMessage)
    {
        var pendingConfig = currentConfig with
        {
            Capture = currentConfig.Capture with
            {
                CameraIndex = cameraIndex
            }
        };

        try
        {
            var savedPath = configWriter.Save(pendingConfig, currentConfig.SourcePath);
            updatedConfig = pendingConfig with { SourcePath = savedPath };
            errorMessage = null;
            logger.LogInformation("Updated camera selection to {CameraIndex} and saved config to {ConfigPath}", cameraIndex, savedPath);
            return true;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to save camera selection {CameraIndex}", cameraIndex);
            updatedConfig = currentConfig;
            errorMessage = exception.Message;
            return false;
        }
    }
}
