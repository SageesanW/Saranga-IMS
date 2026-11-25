using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Saranga.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenCashLedgerTriggersAndRoleCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Harden CashLedgerEntries immutability trigger
            // Disallow bypass for standard application users. Only superusers (postgres / supabase_admin) in explicit maintenance mode can clean up during test maintenance.
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION trg_prevent_cash_ledger_entries_mutation()
                RETURNS TRIGGER AS $$
                BEGIN
                    IF (CURRENT_USER IN ('postgres', 'supabase_admin')) AND current_setting('saranga.maintenance_mode', true) = 'true' THEN
                        IF TG_OP = 'DELETE' THEN RETURN OLD; ELSE RETURN NEW; END IF;
                    END IF;
                    RAISE EXCEPTION 'CashLedgerEntries are immutable: updates and deletions are strictly forbidden at the database level.';
                END;
                $$ LANGUAGE plpgsql;
            ");

            // Harden CashLedgerTransactions immutability trigger
            // Disallow bypass for standard application users; protect ReversalOfTransactionId and enforce strict status transition lifecycle.
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION trg_prevent_cash_ledger_transactions_mutation()
                RETURNS TRIGGER AS $$
                BEGIN
                    IF (CURRENT_USER IN ('postgres', 'supabase_admin')) AND current_setting('saranga.maintenance_mode', true) = 'true' THEN
                        IF TG_OP = 'DELETE' THEN RETURN OLD; ELSE RETURN NEW; END IF;
                    END IF;

                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'CashLedgerTransactions are immutable: deletions are strictly forbidden at the database level.';
                    ELSIF TG_OP = 'UPDATE' THEN
                        -- If already reversed, no further updates of any kind are permitted
                        IF OLD.""Status"" = 2 THEN
                            RAISE EXCEPTION 'Reversed CashLedgerTransactions are permanently final and cannot be modified.';
                        END IF;

                        -- Status can only transition to Reversed (2)
                        IF NEW.""Status"" <> 2 THEN
                            RAISE EXCEPTION 'CashLedgerTransaction status can only transition to Reversed.';
                        END IF;

                        -- ReversalOfTransactionId is strictly immutable after insertion
                        IF (NEW.""ReversalOfTransactionId"" IS DISTINCT FROM OLD.""ReversalOfTransactionId"") THEN
                            RAISE EXCEPTION 'ReversalOfTransactionId is immutable after insertion and cannot be modified.';
                        END IF;

                        -- All core financial and attribution fields are immutable
                        IF NEW.""Id"" <> OLD.""Id"" OR
                           NEW.""TransactionReference"" <> OLD.""TransactionReference"" OR
                           NEW.""BranchId"" <> OLD.""BranchId"" OR
                           NEW.""TransactionType"" <> OLD.""TransactionType"" OR
                           NEW.""SourceType"" <> OLD.""SourceType"" OR
                           (NEW.""SourceReferenceId"" IS DISTINCT FROM OLD.""SourceReferenceId"") OR
                           NEW.""Amount"" <> OLD.""Amount"" OR
                           NEW.""CurrencyCode"" <> OLD.""CurrencyCode"" OR
                           (NEW.""CategoryId"" IS DISTINCT FROM OLD.""CategoryId"") OR
                           (NEW.""IdempotencyKey"" IS DISTINCT FROM OLD.""IdempotencyKey"") OR
                           NEW.""CreatedAt"" <> OLD.""CreatedAt"" OR
                           NEW.""CreatedBy"" <> OLD.""CreatedBy"" THEN
                            RAISE EXCEPTION 'Core financial fields of CashLedgerTransactions are immutable at the database level.';
                        END IF;
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert to initial trigger functions
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION trg_prevent_cash_ledger_entries_mutation()
                RETURNS TRIGGER AS $$
                BEGIN
                    IF current_setting('saranga.bypass_immutability_for_test', true) = 'true' THEN
                        IF TG_OP = 'DELETE' THEN RETURN OLD; ELSE RETURN NEW; END IF;
                    END IF;
                    RAISE EXCEPTION 'CashLedgerEntries are immutable: updates and deletions are strictly forbidden at the database level.';
                END;
                $$ LANGUAGE plpgsql;

                CREATE OR REPLACE FUNCTION trg_prevent_cash_ledger_transactions_mutation()
                RETURNS TRIGGER AS $$
                BEGIN
                    IF current_setting('saranga.bypass_immutability_for_test', true) = 'true' THEN
                        IF TG_OP = 'DELETE' THEN RETURN OLD; ELSE RETURN NEW; END IF;
                    END IF;
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'CashLedgerTransactions are immutable: deletions are strictly forbidden at the database level.';
                    ELSIF TG_OP = 'UPDATE' THEN
                        IF NEW.""Id"" <> OLD.""Id"" OR
                           NEW.""TransactionReference"" <> OLD.""TransactionReference"" OR
                           NEW.""BranchId"" <> OLD.""BranchId"" OR
                           NEW.""TransactionType"" <> OLD.""TransactionType"" OR
                           NEW.""SourceType"" <> OLD.""SourceType"" OR
                           (NEW.""SourceReferenceId"" IS DISTINCT FROM OLD.""SourceReferenceId"") OR
                           NEW.""Amount"" <> OLD.""Amount"" OR
                           NEW.""CurrencyCode"" <> OLD.""CurrencyCode"" OR
                           (NEW.""CategoryId"" IS DISTINCT FROM OLD.""CategoryId"") OR
                           NEW.""CreatedAt"" <> OLD.""CreatedAt"" OR
                           NEW.""CreatedBy"" <> OLD.""CreatedBy"" THEN
                            RAISE EXCEPTION 'Core financial fields of CashLedgerTransactions are immutable at the database level.';
                        END IF;
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
            ");
        }
    }
}
