using Microsoft.EntityFrameworkCore;
using Parking.Entities;

namespace Parking.Data;

public class ParkFlowDbContext : DbContext
{
    public ParkFlowDbContext(DbContextOptions<ParkFlowDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<AppModule> AppModules => Set<AppModule>();
    public DbSet<AppPermission> AppPermissions => Set<AppPermission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<CommercialAgreement> CommercialAgreements => Set<CommercialAgreement>();
    public DbSet<VehicleRate> VehicleRates => Set<VehicleRate>();
    public DbSet<ParkingTicket> ParkingTickets => Set<ParkingTicket>();
    public DbSet<TicketDiscount> TicketDiscounts => Set<TicketDiscount>();
    public DbSet<WorkShift> WorkShifts => Set<WorkShift>();
    public DbSet<CashWithdrawal> CashWithdrawals => Set<CashWithdrawal>();
    public DbSet<PendingSyncItem> PendingSyncItems => Set<PendingSyncItem>();
    public DbSet<PaymentMethodEntity> PaymentMethods => Set<PaymentMethodEntity>();
    public DbSet<MonthlySubscription> MonthlySubscriptions => Set<MonthlySubscription>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<UserBranch> UserBranches => Set<UserBranch>();
    public DbSet<BranchPaymentMethodEntity> BranchPaymentMethods => Set<BranchPaymentMethodEntity>();
    public DbSet<VehicleIncident> VehicleIncidents => Set<VehicleIncident>();
    public DbSet<VehicleIncidentBranch> VehicleIncidentBranches => Set<VehicleIncidentBranch>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ParkFlowDbContext).Assembly);
    }
}
