using EleganceStudio.API.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EleganceStudio.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BarbersController : ControllerBase
{
    private readonly AppDbContext _db;

    public BarbersController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var barbers = await _db.Barbers
            .Where(b => b.IsActive)
            .Select(b => new { b.Id, b.Name })
            .ToListAsync();

        return Ok(barbers);
    }
}
