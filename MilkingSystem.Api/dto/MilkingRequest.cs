using System;

namespace MilkingSystem.Api.dto
{
    // DTO class representing a milking request, containing information about the animal, robot, milk yield, duration, and timestamp of the milking event.
    public class MilkingRequest
    {
        public int AnimalId { get; set; }
        public int RobotId { get; set; }
        public double MilkYieldLiters { get; set; }
        public TimeSpan? Duration { get; set; }
        public DateTime? Timestamp { get; set; }
    }
}