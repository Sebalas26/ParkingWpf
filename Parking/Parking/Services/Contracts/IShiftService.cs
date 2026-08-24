using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Parking.Entities;
using Parking.Models.ApiModels;

namespace Parking.Services.Contracts;

public interface IShiftService
{
    WorkShift? CurrentShift { get; }
    bool HasActiveShift { get; }
    event Action? ShiftStateChanged;

    Task<WorkShift> OpenShiftAsync(decimal baseAmount, string? notes = null);
    Task<WorkShift?> GetActiveShiftAsync();
    Task<ShiftSummaryModel> GetCurrentShiftSummaryAsync();
    Task<WorkShift?> CloseShiftAsync(decimal actualCashCounted, string? notes = null, Guid? handoverToUserId = null, string? handoverToUserName = null);
    Task<WorkShift> HandoverAndOpenNextShiftAsync(decimal actualCashCounted, string? notes, Guid handoverToUserId, string handoverToUserName, decimal newShiftBaseAmount);
    Task<CashWithdrawal> RegisterCashWithdrawalAsync(Guid shiftId, decimal amount, string reason, string authorizedByAdminName, string cashierName);
    Task<IReadOnlyList<CashWithdrawal>> GetShiftCashWithdrawalsAsync(Guid shiftId);
    Task<IReadOnlyList<WorkShift>> GetShiftHistoryAsync(DateTime? fromDate = null, DateTime? toDate = null);
}
