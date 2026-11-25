namespace Saranga.Domain.Constants;

public static class Permissions
{
    // Module: Administration - Branches
    public const string BranchView = "BRANCH_VIEW";
    public const string BranchCreate = "BRANCH_CREATE";
    public const string BranchEdit = "BRANCH_EDIT";
    public const string BranchToggleStatus = "BRANCH_TOGGLE_STATUS";

    // Module: Administration - Users
    public const string UserView = "USER_VIEW";
    public const string UserCreate = "USER_CREATE";
    public const string UserEdit = "USER_EDIT";
    public const string UserToggleStatus = "USER_TOGGLE_STATUS";
    public const string UserAssignRoles = "USER_ASSIGN_ROLES";
    public const string UserAssignBranches = "USER_ASSIGN_BRANCHES";
    public const string UserResetPassword = "USER_RESET_PASSWORD";

    // Module: Administration - Roles
    public const string RoleView = "ROLE_VIEW";
    public const string RoleCreate = "ROLE_CREATE";
    public const string RoleEdit = "ROLE_EDIT";
    public const string RoleAssignPermissions = "ROLE_ASSIGN_PERMISSIONS";

    // Module: Administration - Permissions
    public const string PermissionView = "PERMISSION_VIEW";

    // Module: Attributes Master
    public const string AttributesView = "ATTRIBUTES_VIEW";
    public const string AttributesCreate = "ATTRIBUTES_CREATE";
    public const string AttributesEdit = "ATTRIBUTES_EDIT";
    public const string AttributesActivateDeactivate = "ATTRIBUTES_ACTIVATE_DEACTIVATE";
    public const string AttributeView = AttributesView;
    public const string AttributeManage = AttributesEdit;

    // Module: Inventory
    public const string InventoryView = "INVENTORY_VIEW";
    public const string InventoryCreate = "INVENTORY_CREATE";
    public const string InventoryEdit = "INVENTORY_EDIT";
    public const string InventoryStatusOverride = "INVENTORY_STATUS_OVERRIDE";
    public const string InventoryDelete = "INVENTORY_DELETE";
    public const string InventoryBulkImport = "INVENTORY_BULK_IMPORT";
    public const string InventoryCount = "INVENTORY_COUNT";
    public const string InventoryCountManage = InventoryCount;
    public const string InventoryVarianceResolve = "INVENTORY_VARIANCE_RESOLVE";
    public const string InventoryTransfer = "INVENTORY_TRANSFER";
    public const string InventoryCompositionCreate = "INVENTORY_COMPOSITION_CREATE";
    public const string InventoryCompositionEdit = "INVENTORY_COMPOSITION_EDIT";
    public const string InventoryCompositionDissolve = "INVENTORY_COMPOSITION_DISSOLVE";

    // Module: Rates & VAT
    public const string RateView = "RATE_VIEW";
    public const string RateManage = "RATE_MANAGE";
    public const string VatConfig = "VAT_CONFIG";
    public const string RatesView = RateView;
    public const string RatesManage = RateManage;
    public const string VatView = VatConfig;
    public const string VatManage = VatConfig;

    // Module: Customers
    public const string CustomerView = "CUSTOMER_VIEW";
    public const string CustomerCreate = "CUSTOMER_CREATE";
    public const string CustomerEdit = "CUSTOMER_EDIT";
    public const string CustomerActivateDeactivate = "CUSTOMER_ACTIVATE_DEACTIVATE";

    // Module: POS & Orders / Repairs
    public const string PosView = "POS_VIEW";
    public const string PosCreateSale = "POS_CREATE_SALE";
    public const string PosPriceAdjust = "POS_PRICE_ADJUST";
    public const string PosReturn = "POS_RETURN";
    public const string OrderManage = "ORDER_MANAGE";
    public const string RepairManage = "REPAIR_MANAGE";

    // Module: Shift & Cash Ledger
    public const string ShiftStart = "SHIFT_START";
    public const string ShiftEnd = "SHIFT_END";
    public const string CashLedgerView = "CASH_LEDGER_VIEW";
    public const string CashLedgerAdjust = "CASH_LEDGER_ADJUST";

    // Module: Sales Verification
    public const string SalesVerifyL1 = "SALES_VERIFY_L1";
    public const string SalesVerifyL2 = "SALES_VERIFY_L2";
    public const string SalesFlagReview = "SALES_FLAG_REVIEW";
    public const string SalesVerificationToggle = "SALES_VERIFICATION_TOGGLE";

    // Module: Audit
    public const string AuditView = "AUDIT_VIEW";

    public static readonly IReadOnlyList<PermissionDefinition> All = new List<PermissionDefinition>
    {
        // Administration - Branches
        new(BranchView, "View branch details and listings", "Administration"),
        new(BranchCreate, "Create new branch locations", "Administration"),
        new(BranchEdit, "Edit branch configurations", "Administration"),
        new(BranchToggleStatus, "Activate or deactivate branches", "Administration"),

        // Administration - Users
        new(UserView, "View user accounts and profiles", "Administration"),
        new(UserCreate, "Create new user accounts", "Administration"),
        new(UserEdit, "Edit user details", "Administration"),
        new(UserToggleStatus, "Activate or deactivate user accounts", "Administration"),
        new(UserAssignRoles, "Assign roles to users", "Administration"),
        new(UserAssignBranches, "Assign accessible branches to users", "Administration"),
        new(UserResetPassword, "Reset passwords for user accounts", "Administration"),

        // Administration - Roles
        new(RoleView, "View roles and their permission assignments", "Administration"),
        new(RoleCreate, "Create custom roles", "Administration"),
        new(RoleEdit, "Edit custom role definitions", "Administration"),
        new(RoleAssignPermissions, "Configure permissions for roles", "Administration"),

        // Administration - Permissions
        new(PermissionView, "View system permission catalog", "Administration"),

        // Attributes Master
        new(AttributesView, "View jewellery attribute metadata and lookup catalogs", "Attributes"),
        new(AttributesCreate, "Create new materials, categories, purities, specifications, and locations", "Attributes"),
        new(AttributesEdit, "Edit attribute details and definitions", "Attributes"),
        new(AttributesActivateDeactivate, "Activate or deactivate jewellery attributes and locations", "Attributes"),

        // Inventory
        new(InventoryView, "View inventory items and stock levels", "Inventory"),
        new(InventoryCreate, "Add single items to inventory", "Inventory"),
        new(InventoryEdit, "Edit item specifications and pricing", "Inventory"),
        new(InventoryStatusOverride, "Administrative status correction between Available and Reserved with mandatory audit reason", "Inventory"),
        new(InventoryDelete, "Delete or archive inventory records", "Inventory"),
        new(InventoryBulkImport, "Import inventory via CSV/Excel", "Inventory"),
        new(InventoryCountManage, "Initiate and manage inventory stock counts", "Inventory"),
        new(InventoryVarianceResolve, "Resolve stock count variances", "Inventory"),
        new(InventoryTransfer, "Transfer inventory between branches", "Inventory"),
        new(InventoryCompositionCreate, "Create assembly and group compositions", "Inventory"),
        new(InventoryCompositionEdit, "Add or remove members from assembly and group compositions", "Inventory"),
        new(InventoryCompositionDissolve, "Dissolve assembly and group compositions", "Inventory"),

        // Rates & VAT
        new(RateView, "View gold/silver daily rates and historical board", "Rates & VAT"),
        new(RateManage, "Update daily metal rates", "Rates & VAT"),
        new(VatConfig, "Configure VAT rules and exemption thresholds", "Rates & VAT"),

        // Customers
        new(CustomerView, "View customer profiles and history", "Customers"),
        new(CustomerCreate, "Create new customer records", "Customers"),
        new(CustomerEdit, "Edit customer contact and profile details", "Customers"),
        new(CustomerActivateDeactivate, "Activate or deactivate customer accounts", "Customers"),

        // POS
        new(PosView, "Access point-of-sale terminal", "POS"),
        new(PosCreateSale, "Create sales transactions and invoices", "POS"),
        new(PosPriceAdjust, "Apply authorized discounts and price adjustments", "POS"),
        new(PosReturn, "Process product returns and exchanges", "POS"),
        new(OrderManage, "Create and manage custom jewellery orders", "POS"),
        new(RepairManage, "Accept, track, and deliver customer repairs", "POS"),

        // Shift & Cash
        new(ShiftStart, "Open cashier operational shift", "Shift & Cash"),
        new(ShiftEnd, "Close and reconcile cashier shift", "Shift & Cash"),
        new(CashLedgerView, "View branch cash ledger entries", "Shift & Cash"),
        new(CashLedgerAdjust, "Perform cash-in / cash-out ledger adjustments", "Shift & Cash"),

        // Verification
        new(SalesVerifyL1, "Perform Level-1 post-sale verification", "Sales Verification"),
        new(SalesVerifyL2, "Perform Level-2 high-value sale verification", "Sales Verification"),
        new(SalesFlagReview, "Review and resolve flagged sales transactions", "Sales Verification"),
        new(SalesVerificationToggle, "Configure branch verification policy requirements", "Sales Verification"),

        // Audit
        new(AuditView, "View system audit logs and change histories", "Audit")
    };
}

public record PermissionDefinition(string Name, string Description, string Module);
