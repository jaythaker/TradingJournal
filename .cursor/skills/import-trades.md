---
name: import-trades
description: "Import trades and dividends from CSV files into TradingJournal"
auto-activates:
  - "import trades"
  - "import csv"
  - "upload fidelity"
  - "import dividends"
  - "parse csv"
---

# Skill: Import Trades

## When to Use This Skill

This skill activates when you need to:
- Import trades from Fidelity CSV exports
- Import dividends from broker statements
- Troubleshoot import errors
- Add support for new broker formats

## Prerequisites

- TradingJournal API running
- User account created
- Trading account created in the app
- CSV file from broker

## Step-by-Step Workflow

### Step 1: Export from Broker

**Fidelity:**
1. Log in to Fidelity.com
2. Go to Accounts & Trade → Activity & Orders
3. Click "Download" or "Export"
4. Select CSV format
5. Choose date range

**Expected columns:**
```
Run Date,Action,Symbol,Security Description,Security Type,Quantity,Price ($),Commission ($),Fees ($),Amount ($)
```

### Step 2: Import via Web UI

1. Open http://localhost:5026
2. Log in to your account
3. Navigate to **Import** page
4. Select your trading account
5. Choose the CSV file
6. Click **Import**

### Step 3: Review Import Results

The import shows:
- ✅ Trades imported: X
- ✅ Dividends imported: X
- ⚠️ Skipped (duplicates): X
- ❌ Errors: X

### Step 4: Verify Imported Data

- Check **Trades** page for imported trades
- Check **Dividends** page for imported dividends
- Review **Dashboard** for updated metrics

## Import via CLI

```bash
# Login first
export TJ_API_URL=http://localhost:5122
export TJ_TOKEN=$(curl -s -X POST "$TJ_API_URL/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"you@example.com","password":"yourpassword"}' \
  | jq -r '.token')

# Get account ID
curl -s -H "Authorization: Bearer $TJ_TOKEN" "$TJ_API_URL/api/accounts" | jq

# Import trades
tj import trades --file ~/Downloads/fidelity-export.csv --account <account-id>
```

## Supported Formats

### Fidelity
- Trades: BUY, SELL, options trades
- Dividends: DIVIDEND RECEIVED, REINVESTMENT
- Options: Parsed from OCC symbol format

### Generic CSV
Required columns:
```
Symbol,Type,Quantity,Price,Fee,Date,Notes
AAPL,BUY,100,150.50,4.95,2024-01-15,First purchase
```

## Options Import

Options are detected by symbol format:
```
-AAPL250117C00150000
     │     │  │
     │     │  └── Strike $150.00
     │     └── Call option
     └── Expires Jan 17, 2025
```

**Trade types detected:**
- `BUY_TO_OPEN` - Opening long position
- `SELL_TO_OPEN` - Opening short position (writing)
- `BUY_TO_CLOSE` - Closing short position
- `SELL_TO_CLOSE` - Closing long position

**Spreads detected:**
- Credit spreads (receive premium)
- Debit spreads (pay premium)
- Iron condors, straddles, etc.

## Common Issues and Solutions

**Issue:** All rows skipped as duplicates
**Solution:** Trades already imported. Clear trades first if re-importing:
```bash
# Via API
curl -X DELETE -H "Authorization: Bearer $TJ_TOKEN" \
  "$TJ_API_URL/api/accounts/<account-id>/trades"
```

**Issue:** Date parsing errors
**Solution:** Ensure dates are in recognizable format (MM/DD/YYYY or YYYY-MM-DD)

**Issue:** Options not detected
**Solution:** Check symbol format matches OCC standard. Verify option columns:
- instrumentType should be "Option"
- optionType should be "Call" or "Put"

**Issue:** Negative quantities
**Solution:** Import service handles this automatically (uses absolute value)

**Issue:** Wrong account selected
**Solution:** Re-import to correct account. Delete trades from wrong account first.

## Adding New Broker Support

1. Create `NewBrokerImporter.cs` in `Services/Import/`
2. Implement `ITradeImporter` interface
3. Add to `ImportService` constructor
4. Map CSV columns to Trade model

```csharp
public class NewBrokerImporter : ITradeImporter
{
    public string FormatName => "NewBroker";
    public string FormatDescription => "NewBroker CSV Export";
    
    public bool CanParse(string[] headers)
    {
        return headers.Any(h => h.Contains("NewBroker-Specific-Header"));
    }
    
    public async Task<ImportResult> ImportAsync(...)
    {
        // Parse CSV and create Trade entities
    }
}
```

## Success Criteria

- ✅ CSV file uploaded successfully
- ✅ Trades appear in Trades list
- ✅ Dividends appear in Dividends list
- ✅ Options trades show correct strike/expiration
- ✅ Spreads grouped correctly
- ✅ Dashboard metrics updated
