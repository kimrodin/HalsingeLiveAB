using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventCatalog.Features.Events.Images;

[ApiController]
[Route("/[action]")]
public sealed class EventImagesController(UploadEventImageHandler upload, GetEventImageHandler get,
    DeleteEventImageHandler delete, ILogger<EventImagesController> logger) : ControllerBase
{
    [Authorize(Policy = "AdminOnly")]
    [HttpPost("{eventId:guid}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<IActionResult> UploadEventImage(Guid eventId, [FromForm] UploadEventImageRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await upload.Handle(eventId, request, cancellationToken);
            return result is null ? NotFound() : Created(result.DownloadUrl, result);
        }
        catch (EventValidationException ex) { return Invalid(ex); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { return Conflict(new ProblemDetails { Title = "The image was changed concurrently. Reload and try again." }); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not upload event image for {EventId}", eventId);
            return Problem(title: "Could not upload the event image.", statusCode: 500);
        }
    }

    [HttpGet("{eventId:guid}/{imageId:guid}")]
    public async Task<IActionResult> DownloadEventImage(Guid eventId, Guid imageId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await get.Handle(eventId, imageId, cancellationToken);
            if (result is null) return NotFound();
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(result.Content, result.ContentType);
        }
        catch (EventValidationException ex) { return Invalid(ex); }
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpDelete("{eventId:guid}")]
    public async Task<IActionResult> DeleteEventImage(Guid eventId, CancellationToken cancellationToken)
    {
        try { return await delete.Handle(eventId, cancellationToken) ? NoContent() : NotFound(); }
        catch (EventValidationException ex) { return Invalid(ex); }
    }

    private IActionResult Invalid(EventValidationException exception)
    {
        foreach (var (field, messages) in exception.Errors)
            foreach (var message in messages) ModelState.AddModelError(field, message);
        return ValidationProblem(ModelState);
    }
}
