using Autofac;
using Microsoft.AspNetCore.Mvc;
using MilkingSystem.Core.Services;
using MilkingSystem.Core.Models;

namespace MilkingSystem.Api.Controllers;

/// <summary>
/// Controller for weight measurements.
/// 
/// TODO: Candidates should implement POST endpoint for recording weight measurements.
/// 
/// Requirements:
/// - Accept weight data from robots (animalId, robotId, weightKg)
/// - Validate that animal and robot exist
/// - There is no "double-weighing" protection needed (unlike milking)
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class WeightsController : ControllerBase
{
    private readonly DataService _dataService;

    public WeightsController(DataService dataService)
    {
        _dataService = dataService;
    }

    [HttpGet("animal/{animalId}")]
    public IActionResult GetForAnimal(int animalId)
    {
        var measurements = _dataService.GetWeightMeasurementsForAnimal(animalId);
        return Ok(measurements);
    }

    [HttpGet("animal/{animalId}/last")]
    public IActionResult GetLastForAnimal(int animalId)
    {
        var lastMeasurement = _dataService.GetLastWeightForAnimal(animalId);

        if (lastMeasurement == null)
            return NotFound();

        return Ok(lastMeasurement);
    }

    [HttpPost]
    public IActionResult RecordWeight([FromBody] RecordWeightRequest request)
    {
        if (request == null ||
            request.AnimalId <= 0 ||
            request.RobotId <= 0 ||
            request.WeightKg < 0)
        {
            return BadRequest("Invalid weight measurement request data.");
        }

        // Validate that the animal exists.
        var animal = _dataService.GetAnimalById(request.AnimalId);

        if (animal == null)
            return BadRequest($"Animal {request.AnimalId} not found.");

        // Validate that the robot exists.
        var robot = _dataService.GetRobotById(request.RobotId);

        if (robot == null)
            return BadRequest($"Robot {request.RobotId} not found.");

        var timestamp = request.Timestamp ?? DateTime.UtcNow;

        var generatedId = _dataService.SaveWeightMeasurement(
            request.AnimalId,
            request.RobotId,
            timestamp,
            request.WeightKg);

        return Ok(new
        {
            id = generatedId,
            request.AnimalId,
            request.RobotId,
            request.WeightKg,
            Timestamp = timestamp
        });
    }
}

/// <summary>
/// Request model for recording a weight measurement.
/// </summary>
public class RecordWeightRequest
{
    /// <summary>
    /// The ID of the animal being weighed.
    /// </summary>
    public int AnimalId { get; set; }

    /// <summary>
    /// The ID of the robot performing the weighing.
    /// </summary>
    public int RobotId { get; set; }

    /// <summary>
    /// The weight of the animal in kilograms.
    /// </summary>
    public decimal WeightKg { get; set; }

    /// <summary>
    /// The timestamp of the measurement. If not provided, current UTC time will be used.
    /// </summary>
    public DateTime? Timestamp { get; set; }
}