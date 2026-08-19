using System.Threading.Tasks;
using Parking.Entities;
using Parking.Models;
using Parking.Models.ApiModels;

namespace Parking.Services.Contracts;

public interface IApiClientService
{
    string BaseUrl { get; set; }
    Task<bool> PingAsync();
    Task<BootstrapSyncResponse?> GetBootstrapAsync();
    Task<ParkingTicket?> CheckInAsync(CheckInApiRequest request);
    Task<ParkingTicket?> CheckOutAsync(CheckOutApiRequest request);
    Task<FinancialSummary?> GetFinancialSummaryAsync();
    Task<LoginApiResponse?> LoginAsync(string username, string password);
}
