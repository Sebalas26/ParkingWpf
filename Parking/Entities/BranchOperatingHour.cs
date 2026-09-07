using System;

namespace Parking.Entities;

public class BranchOperatingHour
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public bool IsOpen { get; set; } = true;
    public TimeSpan OpeningTime { get; set; } = new TimeSpan(8, 0, 0);
    public TimeSpan ClosingTime { get; set; } = new TimeSpan(22, 0, 0);
    public int BufferMinutesBefore { get; set; } = 30;
    public int BufferMinutesAfter { get; set; } = 30;

    public virtual Branch? Branch { get; set; }
}
