---
name: database-migration
description: "Create and apply Entity Framework Core migrations for TradingJournal"
auto-activates:
  - "add migration"
  - "create migration"
  - "update database"
  - "schema change"
  - "add column"
---

# Skill: Database Migration

## When to Use This Skill

This skill activates when you need to:
- Add new columns or tables to the database
- Create EF Core migrations for model changes
- Apply pending migrations
- Fix migration issues

## Prerequisites

- TradingJournal.Api project builds successfully
- PostgreSQL database is running
- EF Core tools installed

## Step-by-Step Workflow

### Step 1: Make Model Changes

Edit the model in `TradingJournal.Api/Models/`:

```csharp
[Table("trades")]
public class Trade
{
    // Existing fields...
    
    // New field example
    [Column("newField")]
    public string? NewField { get; set; }
}
```

### Step 2: Create Migration

```bash
cd TradingJournal.Api

# Create migration with descriptive name
dotnet ef migrations add AddNewFieldToTrades
```

**Migration file created in:** `Migrations/[timestamp]_AddNewFieldToTrades.cs`

### Step 3: Review Migration

Check the generated migration:

```csharp
public partial class AddNewFieldToTrades : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "newField",
            table: "trades",
            type: "text",
            nullable: true);
    }
    
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "newField",
            table: "trades");
    }
}
```

### Step 4: Apply Migration

**Option A: Via EF Core (recommended for new databases)**
```bash
dotnet ef database update
```

**Option B: Direct SQL (for existing databases with data)**
```bash
docker exec trading-journal-db psql -U tradingjournal -d tradingjournal -c "
ALTER TABLE trades ADD COLUMN IF NOT EXISTS \"newField\" TEXT;
"
```

### Step 5: Verify Changes

```bash
# Check table schema
docker exec trading-journal-db psql -U tradingjournal -d tradingjournal -c "
\d trades
"

# Or query columns
docker exec trading-journal-db psql -U tradingjournal -d tradingjournal -c "
SELECT column_name, data_type 
FROM information_schema.columns 
WHERE table_name = 'trades'
ORDER BY ordinal_position;
"
```

## Common Issues and Solutions

**Issue:** `relation already exists` error
**Solution:** Tables exist but migration history is missing. Apply SQL directly:
```bash
docker exec trading-journal-db psql -U tradingjournal -d tradingjournal -c "
-- Add columns manually
ALTER TABLE trades ADD COLUMN IF NOT EXISTS \"newColumn\" TEXT;

-- Update migration history (if needed)
INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\")
VALUES ('20260127_MigrationName', '10.0.0')
ON CONFLICT DO NOTHING;
"
```

**Issue:** Foreign key constraint errors
**Solution:** Ensure related records exist or use `ON DELETE CASCADE`:
```csharp
modelBuilder.Entity<Trade>()
    .HasOne(t => t.Account)
    .WithMany()
    .OnDelete(DeleteBehavior.Cascade);
```

**Issue:** Datetime timezone errors
**Solution:** Always use UTC:
```csharp
// In model
[Column("date")]
public DateTime Date { get; set; }

// When querying
var utcDate = DateTime.SpecifyKind(inputDate, DateTimeKind.Utc);
```

**Issue:** Need to rollback migration
**Solution:**
```bash
# Rollback to specific migration
dotnet ef database update PreviousMigrationName

# Remove last migration (if not applied)
dotnet ef migrations remove
```

## Migration Best Practices

1. **Use descriptive names:** `AddOptionsFieldsToTrades` not `Update1`
2. **Small migrations:** One logical change per migration
3. **Review before applying:** Check generated SQL
4. **Backup first:** For production databases
5. **Test locally:** Apply to dev database first

## Success Criteria

- ✅ Migration file generated in `Migrations/` folder
- ✅ Migration applied without errors
- ✅ New columns visible in database schema
- ✅ Application runs without EF Core errors
