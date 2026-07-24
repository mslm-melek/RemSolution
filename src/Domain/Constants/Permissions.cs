namespace RemSolution.Domain.Constants;

/// <summary>
/// Fine-grained permission names, granted per user (UserPermission rows,
/// loaded into the principal as <see cref="Claims.Permission"/> claims at
/// sign-in) and enforced through an authorization policy registered per
/// permission. What a staff member can do is the set of permissions they hold,
/// intersected with the features their agency has enabled (a permission is only
/// effective while its feature is on — see <see cref="FeatureCatalog"/>).
/// AgencyAdministrator holds every permission implicitly.
/// </summary>
public abstract class Permissions
{
    public const string CarCreate = "Car.Create";
    public const string CarRead = "Car.Read";
    public const string CarUpdate = "Car.Update";
    public const string CarDelete = "Car.Delete";

    public const string ClientCreate = "Client.Create";
    public const string ClientRead = "Client.Read";
    public const string ClientUpdate = "Client.Update";
    public const string ClientDelete = "Client.Delete";

    public const string BranchCreate = "Branch.Create";
    public const string BranchRead = "Branch.Read";
    public const string BranchUpdate = "Branch.Update";
    public const string BranchDelete = "Branch.Delete";

    public const string RentingCreate = "Renting.Create";
    public const string RentingRead = "Renting.Read";
    public const string RentingUpdate = "Renting.Update";
    public const string RentingDelete = "Renting.Delete";

    public const string ReservationCreate = "Reservation.Create";
    public const string ReservationRead = "Reservation.Read";
    public const string ReservationUpdate = "Reservation.Update";
    public const string ReservationDelete = "Reservation.Delete";

    public const string ExpenseCreate = "Expense.Create";
    public const string ExpenseRead = "Expense.Read";
    public const string ExpenseUpdate = "Expense.Update";
    public const string ExpenseDelete = "Expense.Delete";

    public const string ExtraServiceCreate = "ExtraService.Create";
    public const string ExtraServiceRead = "ExtraService.Read";
    public const string ExtraServiceUpdate = "ExtraService.Update";
    public const string ExtraServiceDelete = "ExtraService.Delete";

    public const string PaymentCreate = "Payment.Create";
    public const string PaymentRead = "Payment.Read";
    public const string PaymentUpdate = "Payment.Update";
    public const string PaymentDelete = "Payment.Delete";

    public const string ContractGenerate = "Contract.Generate";

    public const string FactureRead = "Facture.Read";
    public const string FactureGenerate = "Facture.Generate";

    public const string CreditRead = "Credit.Read";

    public const string DashboardView = "Dashboard.View";

    public const string ChatView = "Chat.View";

    /// <summary>Every known permission — drives policy registration.</summary>
    public static readonly string[] All =
    {
        CarCreate, CarRead, CarUpdate, CarDelete,
        ClientCreate, ClientRead, ClientUpdate, ClientDelete,
        BranchCreate, BranchRead, BranchUpdate, BranchDelete,
        RentingCreate, RentingRead, RentingUpdate, RentingDelete,
        ReservationCreate, ReservationRead, ReservationUpdate, ReservationDelete,
        ExpenseCreate, ExpenseRead, ExpenseUpdate, ExpenseDelete,
        ExtraServiceCreate, ExtraServiceRead, ExtraServiceUpdate, ExtraServiceDelete,
        PaymentCreate, PaymentRead, PaymentUpdate, PaymentDelete,
        ContractGenerate,
        FactureRead, FactureGenerate,
        CreditRead,
        DashboardView,
        ChatView,
    };

    /// <summary>
    /// The permissions a platform administrator may satisfy while browsing
    /// another tenant read-only through the impersonation path — every read /
    /// view permission, and no create/update/delete/generate.
    /// </summary>
    public static readonly string[] ReadOnly =
    {
        CarRead, ClientRead, BranchRead, RentingRead, ReservationRead,
        ExpenseRead, ExtraServiceRead, PaymentRead, FactureRead, CreditRead,
        DashboardView, ChatView,
    };
}
