using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Insolvex.API.Authorization;
using Insolvex.API.Data;
using Insolvex.Core.Abstractions;
using Insolvex.Core.DTOs;
using Insolvex.Core.Mapping;
using Insolvex.Domain.Entities;
using Insolvex.Domain.Enums;

namespace Insolvex.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[RequirePermission(Permission.TaskView)]
public class TasksController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public TasksController(ApplicationDbContext db, ICurrentUserService currentUser, IAuditService audit)
    {
    _db = db;
     _currentUser = currentUser;
 _audit = audit;
   }

    [HttpGet]
 public async Task<IActionResult> GetAll([FromQuery] Guid? companyId = null, [FromQuery] bool? myTasks = null)
    {
 var query = _db.CompanyTasks
 .Include(t => t.Company)
    .Include(t => t.AssignedTo)
         .AsQueryable();

        if (companyId.HasValue)
        query = query.Where(t => t.CompanyId == companyId);

   if (myTasks == true && _currentUser.UserId.HasValue)
    query = query.Where(t => t.AssignedToUserId == _currentUser.UserId);

   var tasks = await query
       .OrderBy(t => t.Deadline)
      .Select(t => t.ToDto())
        .ToListAsync();

  return Ok(tasks);
    }

   [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
   var task = await _db.CompanyTasks
      .Include(t => t.Company)
      .Include(t => t.AssignedTo)
       .FirstOrDefaultAsync(t => t.Id == id);
        if (task == null) return NotFound();
  return Ok(task.ToDto());
    }

    [HttpPost]
    [RequirePermission(Permission.TaskCreate)]
    public async Task<IActionResult> Create([FromBody] CreateTaskRequest request)
    {
      var companyExists = await _db.Companies.AnyAsync(c => c.Id == request.CompanyId);
   if (!companyExists) return BadRequest("Company not found");

     var task = new CompanyTask
      {
   Id = Guid.NewGuid(),
 CompanyId = request.CompanyId,
    Title = request.Title,
 Description = request.Description,
    Labels = request.Labels,
     Deadline = request.Deadline,
     Status = Domain.Enums.TaskStatus.Open,
          AssignedToUserId = request.AssignedToUserId ?? _currentUser.UserId
  };

        _db.CompanyTasks.Add(task);
   await _db.SaveChangesAsync();
        await _audit.LogAsync("Task.Created", task.Id);
     return CreatedAtAction(nameof(GetById), new { id = task.Id }, task.ToDto());
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permission.TaskEdit)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTaskRequest request)
    {
   var task = await _db.CompanyTasks.FirstOrDefaultAsync(t => t.Id == id);
    if (task == null) return NotFound();

  if (request.Title != null) task.Title = request.Title;
     if (request.Description != null) task.Description = request.Description;
        if (request.Labels != null) task.Labels = request.Labels;
      if (request.Deadline.HasValue) task.Deadline = request.Deadline;
    if (request.Status.HasValue) task.Status = request.Status.Value;
    if (request.AssignedToUserId.HasValue) task.AssignedToUserId = request.AssignedToUserId;

      await _db.SaveChangesAsync();
  await _audit.LogAsync("Task.Updated", task.Id);
        return Ok(task.ToDto());
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permission.TaskDelete)]
public async Task<IActionResult> Delete(Guid id)
    {
    var task = await _db.CompanyTasks.FirstOrDefaultAsync(t => t.Id == id);
      if (task == null) return NotFound();

        _db.CompanyTasks.Remove(task);
   await _db.SaveChangesAsync();
        await _audit.LogAsync("Task.Deleted", id);
        return NoContent();
    }
}
