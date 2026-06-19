using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.PlexDirectorySupport;

/// <summary>Manual Plex token sweep endpoints used by the plugin configuration page.</summary>
[ApiController]
[Route("PlexDirectorySupport")]
public class PlexIdSweepController : ControllerBase
{
    private readonly PlexIdSweepService _sweepService;

    /// <summary>Initialises a new instance of the <see cref="PlexIdSweepController"/> class.</summary>
    public PlexIdSweepController(PlexIdSweepService sweepService)
    {
        _sweepService = sweepService;
    }

    /// <summary>Runs a manual Plex token sweep.</summary>
    [HttpPost("Sweep")]
    public async Task<ActionResult<PlexIdSweepResult>> Sweep(
        [FromQuery] bool forceRefresh,
        CancellationToken cancellationToken)
    {
        var result = await _sweepService.RunAsync(
            forceRefresh ? "Manual force" : "Manual",
            forceRefresh,
            refreshMetadata: true,
            progress: null,
            cancellationToken).ConfigureAwait(false);

        return Ok(result);
    }
}
