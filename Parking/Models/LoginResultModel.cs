using System.Collections.Generic;

namespace Parking.Models;

public class LoginResultModel
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public UserSessionModel? User { get; set; }
    public List<BranchModel> Branches { get; set; } = new();
}
