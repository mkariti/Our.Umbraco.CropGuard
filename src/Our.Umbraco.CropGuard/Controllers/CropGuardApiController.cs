using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Our.Umbraco.CropGuard.Models;
using Our.Umbraco.CropGuard.Services;

namespace Our.Umbraco.CropGuard.Controllers;

/// <summary>
/// Management API controller powering the backoffice dashboard.
/// Secured to backoffice users via Umbraco's built-in authorization policy.
/// </summary>
[ApiController]
[Authorize(Policy = "UmbracoBackOfficeDefaultPolicy")]
[Route("umbraco/management/api/v1/cropguard")]
public sealed class CropGuardApiController : ControllerBase
{
    private readonly IAllowedCropService _cropService;

    public CropGuardApiController(IAllowedCropService cropService)
        => _cropService = cropService;

    /// <summary>Returns all currently allowed crops across all sources.</summary>
    [HttpGet("crops")]
    [ProducesResponseType(typeof(IReadOnlyList<CropDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var crops = (await _cropService.GetAllAsync()).Select(CropDto.From);
        return Ok(crops);
    }

    /// <summary>Adds a custom crop and persists it to the database.</summary>
    [HttpPost("crops")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Add([FromBody] AddCropRequest request)
    {
        if (request.Width <= 0 || request.Height <= 0)
            return BadRequest("Width and Height must be positive integers.");

        await _cropService.AddCustomCropAsync(request.Width, request.Height, request.Alias);
        return StatusCode(StatusCodes.Status201Created);
    }

    /// <summary>Removes a custom crop from the database.</summary>
    [HttpDelete("crops/{width:int}/{height:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Remove(int width, int height)
    {
        await _cropService.RemoveCustomCropAsync(width, height);
        return NoContent();
    }

    /// <summary>Forces a cache refresh, re-reading crops from all sources.</summary>
    [HttpPost("crops/refresh")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Refresh()
    {
        _cropService.InvalidateCache();
        return NoContent();
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public sealed record CropDto(int Width, int Height, string? Alias, string Source)
{
    public static CropDto From(AllowedCrop c) => new(c.Width, c.Height, c.Alias, c.Source.ToString());
}

public sealed record AddCropRequest(int Width, int Height, string? Alias);
