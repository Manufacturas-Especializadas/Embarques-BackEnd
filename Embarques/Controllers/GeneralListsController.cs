using Embarques.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Embarques.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GeneralListsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public GeneralListsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Route("GetFletes")]
        public async Task<IActionResult> GetFletes()
        {
            var fletes = await _context.Fletes
                            .AsNoTracking()
                            .OrderByDescending(f => f.Id)
                            .Select(f => new
                            {
                                f.Id,
                                Supplier = f.IdSupplierNavigation.SupplierName,
                                Destination = f.IdDestinationNavigation.DestinationName,
                                f.HighwayExpenseCost,
                                f.CostOfStay,
                                RegistrationDate = f.RegistrationDate!.Value.ToString("dd/MM/yyyy"),
                                IndividualCost = f.IdDestinationNavigation.Cost,
                                Date = f.CreatedAt.Value.ToString("dd/MM/yyyy")
                            })
                            .ToListAsync();

            if(fletes == null)
            {
                return BadRequest("List empty");
            }

            return Ok(fletes);
        }

        [HttpGet]
        [Route("GetSuppliers")]
        public async Task<IActionResult> GetSuppliers()
        {
            var suppliers = await _context.Suppliers
                                .AsNoTracking()
                                .OrderBy(s => s.SupplierName)
                                .ToListAsync();

            if( suppliers == null )
            {
                return BadRequest("List empty");
            }

            return Ok(suppliers);
        }

        [HttpGet]
        [Route("GetDestination")]
        public async Task<IActionResult> GetDestination()
        {
            var destination = await _context.Destination
                                    .AsNoTracking()
                                    .OrderBy(d => d.DestinationName)
                                    .ToListAsync();

            if( destination == null )
            {
                return BadRequest("List empty");
            }

            return Ok(destination);
        }
    }
}