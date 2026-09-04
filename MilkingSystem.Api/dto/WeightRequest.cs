using System;
namespace MilkingSystem.Api.dto
{
    // DTO class representing a weight measurement request, containing information about the animal, robot, weight in kilograms, and timestamp of the measurement.
    public class WeightRequest
    {
        public int AnimalId { get; set; }
        public int RobotId { get; set; }
        public double WeightKg { get; set; }
        public DateTime? Timestamp { get; set; }
    }
}