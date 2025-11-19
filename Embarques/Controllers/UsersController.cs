using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Embarques.Models;

namespace Embarques.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UsersController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Route("GetUsers")]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _context.Users
                            .OrderByDescending(u => u.Rol.Name == "Admin")
                            .AsNoTracking()
                            .Select(u => new
                            {
                                u.Id,
                                u.Name,
                                u.PayRollNumber,
                                RoleName = u.Rol.Name,
                            })
                            .ToListAsync();

            if(users == null)
            {
                return BadRequest("Sin usuarios");
            }

            return Ok(users);
        }

        [HttpDelete]
        [Route("DeleteUser/{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);

            if(user == null)
            {
                return NotFound("Usuario no encontrado");
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Usuario eliminado"
            });
        }
    }
}