using EleganceStudio.API.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EleganceStudio.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ServicesController : ControllerBase
{
    private readonly AppDbContext _db;

    public ServicesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var services = await _db.Services
            .Select(s => new { s.Id, s.Name, s.Price, s.DurationMinutes })
            .ToListAsync();

        return Ok(services);
    }
}
