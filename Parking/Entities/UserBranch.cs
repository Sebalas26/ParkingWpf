using System;

namespace Parking.Entities;

public class UserBranch
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int BranchId { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
}
