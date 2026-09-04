using System;
using Microsoft.AspNetCore.Mvc;
using MilkingSystem.Core.Models;
using MilkingSystem.Core.Services;
using MilkingSystem.Core.Notifications;

namespace MilkingSystem.Api.Controllers;

/// <summary>
/// Controller for milking events.
/// 
/// TODO: Candidates should implement POST endpoint for recording new milking events.
/// 
/// Background:
/// - Robots are large stationary machines in the barn
/// - A cow walks into a robot and gets milked autonomously
/// - After milking, a cow might walk to another robot hoping for more food
/// - Other robots must know NOT to milk this cow (she was recently milked)
/// 
/// Requirements:
/// - Accept milking data from robots (animalId, robotId, milkYieldLiters, duration)
/// - Prevent double-milking: if an animal was milked within the last 6 hours, reject the request
/// - Notify other robots about the milking event using IRobotNotifier
/// - Handle concurrent messages from multiple robots about different cows
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MilkingsController : ControllerBase
{
    
    private readonly IRobotNotifier _robotNotifier;
    private readonly DataService _dataService;
    private static readonly object SyncLock = new object();

    public MilkingsController(DataService dataService, IRobotNotifier robotNotifier)
    {
        _dataService = dataService;
        _robotNotifier = robotNotifier;
        
    }
    

    [HttpGet("animal/{animalId}")]
    public IActionResult GetForAnimal(int animalId)
    {
        var dataService = _dataService;
        var events = dataService.GetMilkingEventsForAnimal(animalId);
        return Ok(events);
    }

    [HttpGet("animal/{animalId}/last")]
    public IActionResult GetLastForAnimal(int animalId)
    {
        var dataService = _dataService;
        var lastEvent = dataService.GetLastMilkingForAnimal(animalId);
        
        if (lastEvent == null)
            return NotFound();
        
        return Ok(lastEvent);
    }

    [HttpGet("recent")]
    public IActionResult GetRecent([FromQuery] int hours = 24)
    {
        var dataService = _dataService;
        var events = dataService.GetRecentMilkingEvents(hours);
        return Ok(events);
    }

    // TODO: Candidate should implement this endpoint
    [HttpPost]
    public IActionResult RecordMilking([FromBody] RecordMilkingRequest request)
    {
        
        if(request == null || request.AnimalId <= 0 || request.RobotId <= 0 || request.MilkYieldLiters < 0)
        {
            return BadRequest("Invalid milking request data.");
        }
        var timestamp = request.Timestamp ?? DateTime.UtcNow;
        //fast path check using in-memory notifier to reject duplicate attempts 
        if(_robotNotifier.WasRecentlyMilked(request.AnimalId))
        {
            return Conflict("Animal was recently milked within last 6 hours.");
        }
        lock(SyncLock)
        {
            var animal = _dataService.GetAnimalById(request.AnimalId);
            if (animal == null)
            {
                return BadRequest($"Animal with ID {request.AnimalId} not found.");
            }

            var robot = _dataService.GetRobotById(request.RobotId);
            if (robot == null || !robot.IsActive)
            {
                return BadRequest($"Robot with ID {request.RobotId} not found or is not active.");
            }

            var lastMilking = _dataService.GetLastMilkingForAnimal(request.AnimalId);
            if (lastMilking != null && (timestamp - lastMilking.Timestamp).TotalHours < 6)
            {
                return Conflict("Animal was recently milked within last 6 hours.");
            }
            decimal yieldInDec = (decimal)request.MilkYieldLiters;
            int? durationInSeconds = request.Duration;
            //int? durationInSeconds = int? durationInSeconds = request.Duration;
            //request.Duration.HasValue ? (int?)request.Duration.Value.TotalSeconds : null;
            int generatedId = _dataService.SaveMilkingEvent(
                request.AnimalId, 
                request.RobotId, 
                timestamp,
                request.MilkYieldLiters, 
                durationInSeconds);
            _robotNotifier.NotifyMilkingCompleted(new MilkingNotification
            {
                AnimalId = request.AnimalId,
                RobotId = request.RobotId,
                Timestamp = timestamp,
                AnimalIdentificationNumber = animal.IdentificationNumber
            });

            return Ok(new
            { id = generatedId,
              request.AnimalId,
              request.RobotId,
              MilkYieldLiters = request.MilkYieldLiters,
              Duration = request.Duration,
              Timestamp = timestamp
            });
            
        }
    }
}

/// <summary>
/// Request model for recording a milking event.
/// </summary>
public class RecordMilkingRequest
{
    /// <summary>
    /// The ID of the animal being milked.
    /// </summary>
    public int AnimalId { get; set; }
    
    /// <summary>
    /// The ID of the robot performing the milking.
    /// </summary>
    public int RobotId { get; set; }
    
    /// <summary>
    /// The amount of milk collected in liters.
    /// </summary>
    public decimal MilkYieldLiters { get; set; }
    
    /// <summary>
    /// The duration of the milking in seconds (optional).
    /// </summary>
    public int? Duration { get; set; }
    
    /// <summary>
    /// The timestamp of the milking event. If not provided, current UTC time will be used.
    /// </summary>
    public DateTime? Timestamp { get; set; }
}
