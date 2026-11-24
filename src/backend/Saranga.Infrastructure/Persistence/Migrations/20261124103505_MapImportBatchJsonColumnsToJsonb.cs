using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Saranga.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MapImportBatchJsonColumnsToJsonb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""InventoryImportBatches"" 
                ALTER COLUMN ""ValidationErrorsJson"" TYPE jsonb 
                USING ""ValidationErrorsJson""::jsonb;

                ALTER TABLE ""InventoryImportBatches"" 
                ALTER COLUMN ""ResultSummaryJson"" TYPE jsonb 
                USING ""ResultSummaryJson""::jsonb;

                ALTER TABLE ""InventoryImportBatches"" 
                ALTER COLUMN ""PayloadJson"" TYPE jsonb 
                USING ""PayloadJson""::jsonb;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""InventoryImportBatches"" 
                ALTER COLUMN ""ValidationErrorsJson"" TYPE text 
                USING ""ValidationErrorsJson""::text;

                ALTER TABLE ""InventoryImportBatches"" 
                ALTER COLUMN ""ResultSummaryJson"" TYPE text 
                USING ""ResultSummaryJson""::text;

                ALTER TABLE ""InventoryImportBatches"" 
                ALTER COLUMN ""PayloadJson"" TYPE text 
                USING ""PayloadJson""::text;
            ");
        }
    }
}
