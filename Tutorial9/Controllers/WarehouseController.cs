using Microsoft.AspNetCore.Mvc;
using Tutorial9.Model;
using Tutorial9.Services;

namespace Tutorial9.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WarehouseController : ControllerBase
{
    private readonly IDbService _dbService;
    
    public WarehouseController(IDbService dbService)
    {
        _dbService = dbService;
    }

    [HttpPost("add")]
    public async Task<IActionResult> AddProductToWarehouse([FromBody] ProductWarehouseRequest request)
    {
        try
        {
            var id = await _dbService.AddProductToWarehouseAsync(request);
            return Ok(new { Id = id });
        }
        catch (Exception e)
        {
            return BadRequest(e.Message);
        }
    }

    [HttpPost("add-procedure")]
    public async Task<IActionResult> AddProductsUsingProcedure([FromBody] ProductWarehouseRequest request)
    {
        try
        {
            var id = await _dbService.AddProductToWarehouseUsingProcedureAsync(request);
            return Ok(new { Id = id });
        }
        catch (Exception e)
        {
            return BadRequest(new { Error = e.Message });
        }
    }
}