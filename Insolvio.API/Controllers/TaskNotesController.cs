using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Insolvio.API.Authorization;
using Insolvio.Core.Abstractions;
using Insolvio.Core.DTOs;
using Insolvio.Domain.Enums;

namespace Insolvio.API.Controllers;

[ApiController]
[Route("api/tasks/{taskId:guid}/notes")]
[Authorize]
public class TaskNotesController : ControllerBase
{
    private readonly ITaskService _tasks;

    public TaskNotesController(ITaskService tasks) => _tasks = tasks;

    [HttpGet]
    public async Task<IActionResult> GetAll(Guid taskId, CancellationToken ct)
        => Ok(await _tasks.GetNotesAsync(taskId, ct));

    [HttpPost]
    public async Task<IActionResult> Add(Guid taskId, [FromBody] AddTaskNoteRequest req, CancellationToken ct)
        => Ok(await _tasks.AddNoteAsync(taskId, req.Content, ct));

    [HttpPut("{noteId:guid}")]
    public async Task<IActionResult> Update(Guid taskId, Guid noteId, [FromBody] UpdateTaskNoteRequest req, CancellationToken ct)
        => Ok(await _tasks.UpdateNoteAsync(noteId, req.Content, ct));

    [HttpDelete("{noteId:guid}")]
    public async Task<IActionResult> Delete(Guid taskId, Guid noteId, CancellationToken ct)
    {
        await _tasks.DeleteNoteAsync(noteId, ct);
        return Ok(new { message = "Deleted" });
    }
}
