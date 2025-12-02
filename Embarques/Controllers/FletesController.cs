using ClosedXML.Excel;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Spreadsheet;
using Embarques.Dtos;
using Embarques.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Embarques.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FletesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public FletesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        [Route("Create")]
        public async Task<IActionResult> Create([FromBody] FletesDto dto)
        {
            if (dto == null)
            {
                return BadRequest("Datos vacios");
            }

            var supplier = await _context.Suppliers.FindAsync(dto.IdSupplier);
            string supplierName = supplier?.SupplierName?.ToUpper() ?? "";

            bool isProveedorSinCosto = supplierName == "UNIDAD MESA" ||
                                        supplierName == "RECOLECCIONES A PROVEEDOR" ||
                                        supplierName == "RECOLECCION POR CLIENTE";

            var newFlete = new Fletes
            {
                IdDestination = dto.IdDestination,
                IdSupplier = dto.IdSupplier,
                HighwayExpenseCost = isProveedorSinCosto ? 0 : dto.HighwayExpenseCost,
                CostOfStay = isProveedorSinCosto ? 0 : dto.CostOfStay,
                RegistrationDate = dto.RegistrationDate,
                TripNumber = dto.TripNumber,
            };

            _context.Fletes.Add(newFlete);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Registrado"
            });
        }

        [HttpPost]
        [Route("GenerateMonthlyReport")]
        public async Task<IActionResult> GenerateMonthlyReport([FromBody] MonthlyReportDto dto)
        {
            try
            {
                if (dto.Month < 1 || dto.Month > 12 || dto.Year < 2000 || dto.Year > 2100)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Mes o año no válido"
                    });
                }

                var fletes = await _context.Fletes
                                .Include(f => f.IdSupplierNavigation)
                                .Include(f => f.IdDestinationNavigation)
                                .Where(f => f.RegistrationDate!.Value.Year == dto.Year && f.RegistrationDate!.Value.Month == dto.Month)
                                .OrderBy(f => f.RegistrationDate)
                                .OrderByDescending(f => f.Id)
                                .AsNoTracking()
                                .ToListAsync();

                if (!fletes.Any())
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "No hay datos para el mes especificado"
                    });
                }

                using (var workbook = new XLWorkbook())
                {
                    var workSheet = workbook.Worksheets.Add("Reporte mensual");

                    var nameMonth = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(dto.Month);
                    workSheet.Cell(1, 1).Value = $"REPORTE DE FLETES - {nameMonth.ToUpper()} {dto.Year}";
                    workSheet.Cell(1, 1).Style.Font.Bold = true;
                    workSheet.Cell(1, 1).Style.Font.FontSize = 14;
                    workSheet.Range(1, 1, 1, 8).Merge();

                    var headers = new string[] { "Proveedor", "Semana", "Destino", "Número de viaje" ,"Costo proveedor", "Gastos autopista", "Gasto estadía", "Costo total", "Fecha" };

                    for (int i = 0; i < headers.Length; i++)
                    {
                        workSheet.Cell(3, i + 1).Value = headers[i];
                        workSheet.Cell(3, i + 1).Style.Font.Bold = true;
                        workSheet.Cell(3, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                    }

                    int row = 4;
                    foreach (var flete in fletes)
                    {
                        workSheet.Cell(row, 1).Value = flete.IdSupplierNavigation?.SupplierName ?? "N/A";

                        var date = flete.RegistrationDate!.Value;
                        var firtsMonth = new DateTime(date.Year, date.Month, 1);
                        var diff = (date - firtsMonth).Days;
                        var week = (diff / 7) + 1;
                        var startWeek = firtsMonth.AddDays((week - 1) * 7);
                        var endWeek = startWeek.AddDays(6);

                        if (endWeek.Month != date.Month)
                        {
                            endWeek = new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));
                        }

                        workSheet.Cell(row, 2).Value = $"{startWeek:dd MMM}.-{endWeek:dd MMM}";

                        workSheet.Cell(row, 3).Value = flete.IdDestinationNavigation?.DestinationName ?? "N/A";
                        workSheet.Cell(row, 4).Value = flete.TripNumber ?? 0;
                        workSheet.Cell(row, 5).Value = flete.IdDestinationNavigation?.Cost ?? 0;

                        workSheet.Cell(row, 6).Value = flete.HighwayExpenseCost ?? 0;

                        workSheet.Cell(row, 7).Value = flete.CostOfStay ?? 0;

                        var totalCost = (flete.IdDestinationNavigation?.Cost ?? 0) +
                                       (flete.HighwayExpenseCost ?? 0) +
                                       (flete.CostOfStay ?? 0);
                        workSheet.Cell(row, 8).Value = totalCost;

                        workSheet.Cell(row, 9).Value = flete.RegistrationDate?.ToString("dd/MM/yyyy") ?? "N/A";

                        row++;
                    }

                    workSheet.Column(5).Style.NumberFormat.Format = "$ #,##0";
                    workSheet.Column(6).Style.NumberFormat.Format = "$ #,##0";
                    workSheet.Column(7).Style.NumberFormat.Format = "$ #,##0";

                    workSheet.Columns().AdjustToContents();

                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        var content = stream.ToArray();

                        var fileName = $"Reporte_Fletes_{nameMonth}_{dto.Year}.xlsx";

                        return File(
                            content,
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            fileName
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Error al generar el reporte: {ex.Message}"
                });
            }
        }

        [HttpPost]
        [Route("GenerateReportByDateRange")]
        public async Task<IActionResult> GenerateReportByDateRange([FromBody] ReportByDateDto dto)
        {
            try
            {
                if (dto.StartTime > dto.EndTime)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "La fecha de inicio no puede ser mayor a la fecha de fin"
                    });
                }

                var fletes = await _context.Fletes
                                .Include(f => f.IdSupplierNavigation)
                                .Include(f => f.IdDestinationNavigation)
                                .Where(f => f.RegistrationDate.HasValue &&
                                            f.RegistrationDate.Value.Date >= dto.StartTime.Date &&
                                            f.RegistrationDate.Value.Date <= dto.EndTime.Date)
                                .OrderBy(f => f.RegistrationDate)
                                .ThenByDescending(f => f.Id)
                                .AsNoTracking()
                                .ToListAsync();

                if (!fletes.Any())
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "No hay datos para el período especificado"
                    });
                }

                using (var workbook = new XLWorkbook())
                {
                    var workSheet = workbook.Worksheets.Add("Reporte por período");

                    workSheet.Cell(1, 1).Value = $"REPORTE DE FLETES - DEL {dto.StartTime:dd/MM/yyyy} AL {dto.EndTime:dd/MM/yyyy}";
                    workSheet.Cell(1, 1).Style.Font.Bold = true;
                    workSheet.Cell(1, 1).Style.Font.FontSize = 14;
                    workSheet.Range(1, 1, 1, 9).Merge();

                    var headers = new string[] { "Proveedor", "Semana", "Destino", "Número de viaje", "Costo proveedor", "Gastos autopista", "Gasto estadía", "Costo total", "Fecha" };

                    for (int i = 0; i < headers.Length; i++)
                    {
                        workSheet.Cell(3, i + 1).Value = headers[i];
                        workSheet.Cell(3, i + 1).Style.Font.Bold = true;
                        workSheet.Cell(3, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                    }

                    int row = 4;
                    foreach (var flete in fletes)
                    {
                        workSheet.Cell(row, 1).Value = flete.IdSupplierNavigation?.SupplierName ?? "N/A";

                        if (flete.RegistrationDate.HasValue)
                        {
                            var date = flete.RegistrationDate.Value;
                            var firtsMonth = new DateTime(date.Year, date.Month, 1);
                            var diff = (date - firtsMonth).Days;
                            var week = (diff / 7) + 1;
                            var startWeek = firtsMonth.AddDays((week - 1) * 7);
                            var endWeek = startWeek.AddDays(6);

                            if (endWeek.Month != date.Month)
                            {
                                endWeek = new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));
                            }

                            workSheet.Cell(row, 2).Value = $"{startWeek:dd MMM}.-{endWeek:dd MMM}";
                        }
                        else
                        {
                            workSheet.Cell(row, 2).Value = "N/A";
                        }

                        workSheet.Cell(row, 3).Value = flete.IdDestinationNavigation?.DestinationName ?? "N/A";
                        workSheet.Cell(row, 4).Value = flete.TripNumber ?? 0;
                        workSheet.Cell(row, 5).Value = flete.IdDestinationNavigation?.Cost ?? 0;
                        workSheet.Cell(row, 6).Value = flete.HighwayExpenseCost ?? 0;
                        workSheet.Cell(row, 7).Value = flete.CostOfStay ?? 0;

                        var totalCost = (flete.IdDestinationNavigation?.Cost ?? 0) +
                                       (flete.HighwayExpenseCost ?? 0) +
                                       (flete.CostOfStay ?? 0);
                        workSheet.Cell(row, 8).Value = totalCost;

                        workSheet.Cell(row, 9).Value = flete.RegistrationDate?.ToString("dd/MM/yyyy") ?? "N/A";

                        row++;
                    }

                    workSheet.Column(5).Style.NumberFormat.Format = "$ #,##0";
                    workSheet.Column(6).Style.NumberFormat.Format = "$ #,##0";
                    workSheet.Column(7).Style.NumberFormat.Format = "$ #,##0";
                    workSheet.Column(8).Style.NumberFormat.Format = "$ #,##0";

                    workSheet.Columns().AdjustToContents();

                    workSheet.Cell(row, 4).Value = "TOTALES:";
                    workSheet.Cell(row, 4).Style.Font.Bold = true;

                    workSheet.Cell(row, 5).FormulaA1 = $"SUM(E4:E{row - 1})";
                    workSheet.Cell(row, 6).FormulaA1 = $"SUM(F4:F{row - 1})";
                    workSheet.Cell(row, 7).FormulaA1 = $"SUM(G4:G{row - 1})";
                    workSheet.Cell(row, 8).FormulaA1 = $"SUM(H4:H{row - 1})";

                    for (int col = 5; col <= 8; col++)
                    {
                        workSheet.Cell(row, col).Style.Font.Bold = true;
                        workSheet.Cell(row, col).Style.Fill.BackgroundColor = XLColor.LightBlue;
                        workSheet.Cell(row, col).Style.NumberFormat.Format = "$ #,##0";
                    }

                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        var content = stream.ToArray();

                        var fileName = $"Reporte_Fletes_{dto.StartTime:yyyyMMdd}_{dto.EndTime:yyyyMMdd}.xlsx";

                        return File(
                            content,
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            fileName
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Error al generar el reporte: {ex.Message}"
                });
            }
        }

        [HttpGet]
        [Route("GeMonthsWithData")]
        public async Task<IActionResult> GeMonthsWithData()
        {
            try
            {
                var monthWithData = await _context.Fletes
                        .Where(f => f.RegistrationDate.HasValue)
                        .Select(f => new
                        {
                            Year = f.RegistrationDate!.Value.Year,
                            Month = f.RegistrationDate.Value.Month,
                        })
                        .Distinct()
                        .OrderByDescending(x => x.Year)
                        .ThenByDescending(x => x.Month)
                        .AsNoTracking()
                        .ToListAsync();

                var result = monthWithData.Select(m => new
                {
                    Year = m.Year,
                    Month = m.Month,
                    MonthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(m.Month),
                    Description = $"{CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(m.Month)}{m.Year}"
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Error al obtener meses: {ex.Message}"
                });
            }
        }

        [HttpGet]
        [Route("GetFletesById/{id}")]
        public async Task<IActionResult> GetFletesById(int id)
        {
            var flete = await _context.Fletes
                    .Where(f => f.Id == id)
                    .Select(f => new
                    {
                        f.Id,
                        Supplier = f.IdSupplierNavigation.SupplierName,
                        Destination = f.IdDestinationNavigation.DestinationName,
                        f.HighwayExpenseCost,
                        f.CostOfStay,
                        f.RegistrationDate,
                        f.TripNumber,
                        IndividualCost = f.IdDestinationNavigation.Cost,
                        IdSupplier = f.IdSupplier,
                        IdDestination = f.IdDestination                        
                    })
                    .FirstOrDefaultAsync();

            if (flete == null)
            {
                return NotFound($"Flete con ID {id} no encontrado");
            }

            return Ok(flete);
        }

        [HttpPut]
        [Route("Update/{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] FletesDto dto)
        {
            var existingFlete = await _context.Fletes.FindAsync(id);
            if (existingFlete == null)
            {
                return NotFound();
            }

            var supplier = await _context.Suppliers.FindAsync(dto.IdSupplier);
            string supplierName = supplier?.SupplierName?.ToUpper() ?? "";

            bool isProveedorSinCosto = supplierName == "UNIDAD MESA" ||
                                      supplierName == "RECOLECCIONES A PROVEEDOR" ||
                                      supplierName == "RECOLECCION POR CLIENTE";

            existingFlete.IdDestination = dto.IdDestination;
            existingFlete.IdSupplier = dto.IdSupplier;
            existingFlete.HighwayExpenseCost = isProveedorSinCosto ? 0 : dto.HighwayExpenseCost;
            existingFlete.CostOfStay = isProveedorSinCosto ? 0 : dto.CostOfStay;
            existingFlete.RegistrationDate = dto.RegistrationDate;
            existingFlete.TripNumber = dto.TripNumber;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Actualizado"
            });
        }

        [HttpDelete]
        [Route("Delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var flete = await _context.Fletes.FindAsync(id);

            if(flete == null)
            {
                return NotFound("Flete no encontrado");
            }

            _context.Fletes.Remove(flete);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Flete eliminado correctamente"
            });
        }
    }
}
