using System;
using System.Collections.Generic;
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
    Task LogoutAsync();
    void SetAuthToken(string token);
    void ClearAuthToken();

    // Endpoints de Turnos y Arqueo de Caja
    Task<WorkShift?> OpenShiftAsync(OpenShiftApiRequest request);
    Task<WorkShift?> GetActiveShiftAsync(int? userId = null);
    Task<ShiftSummaryModel?> GetShiftSummaryAsync(Guid shiftId);
    Task<WorkShift?> CloseShiftAsync(CloseShiftApiRequest request);
    Task<IReadOnlyList<WorkShift>> GetShiftHistoryAsync(DateTime? fromDate = null, DateTime? toDate = null);
}
