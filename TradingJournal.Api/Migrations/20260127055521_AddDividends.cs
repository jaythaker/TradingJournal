using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDividends : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_accounts_users_UserId",
                table: "accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_portfolios_accounts_AccountId",
                table: "portfolios");

            migrationBuilder.DropForeignKey(
                name: "FK_portfolios_users_UserId",
                table: "portfolios");

            migrationBuilder.DropForeignKey(
                name: "FK_trades_accounts_AccountId",
                table: "trades");

            migrationBuilder.DropForeignKey(
                name: "FK_trades_users_UserId",
                table: "trades");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "users",
                newName: "updatedAt");

            migrationBuilder.RenameColumn(
                name: "Password",
                table: "users",
                newName: "password");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "users",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "Email",
                table: "users",
                newName: "email");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "users",
                newName: "createdAt");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "users",
                newName: "id");

            migrationBuilder.RenameIndex(
                name: "IX_users_Email",
                table: "users",
                newName: "IX_users_email");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "trades",
                newName: "userId");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "trades",
                newName: "updatedAt");

            migrationBuilder.RenameColumn(
                name: "Type",
                table: "trades",
                newName: "type");

            migrationBuilder.RenameColumn(
                name: "Symbol",
                table: "trades",
                newName: "symbol");

            migrationBuilder.RenameColumn(
                name: "Quantity",
                table: "trades",
                newName: "quantity");

            migrationBuilder.RenameColumn(
                name: "Price",
                table: "trades",
                newName: "price");

            migrationBuilder.RenameColumn(
                name: "Notes",
                table: "trades",
                newName: "notes");

            migrationBuilder.RenameColumn(
                name: "Fee",
                table: "trades",
                newName: "fee");

            migrationBuilder.RenameColumn(
                name: "Date",
                table: "trades",
                newName: "date");

            migrationBuilder.RenameColumn(
                name: "Currency",
                table: "trades",
                newName: "currency");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "trades",
                newName: "createdAt");

            migrationBuilder.RenameColumn(
                name: "AccountId",
                table: "trades",
                newName: "accountId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "trades",
                newName: "id");

            migrationBuilder.RenameIndex(
                name: "IX_trades_UserId",
                table: "trades",
                newName: "IX_trades_userId");

            migrationBuilder.RenameIndex(
                name: "IX_trades_AccountId",
                table: "trades",
                newName: "IX_trades_accountId");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "portfolios",
                newName: "userId");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "portfolios",
                newName: "updatedAt");

            migrationBuilder.RenameColumn(
                name: "Symbol",
                table: "portfolios",
                newName: "symbol");

            migrationBuilder.RenameColumn(
                name: "Quantity",
                table: "portfolios",
                newName: "quantity");

            migrationBuilder.RenameColumn(
                name: "CurrentPrice",
                table: "portfolios",
                newName: "currentPrice");

            migrationBuilder.RenameColumn(
                name: "AveragePrice",
                table: "portfolios",
                newName: "averagePrice");

            migrationBuilder.RenameColumn(
                name: "AccountId",
                table: "portfolios",
                newName: "accountId");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "portfolios",
                newName: "id");

            migrationBuilder.RenameIndex(
                name: "IX_portfolios_UserId_AccountId_Symbol",
                table: "portfolios",
                newName: "IX_portfolios_userId_accountId_symbol");

            migrationBuilder.RenameIndex(
                name: "IX_portfolios_AccountId",
                table: "portfolios",
                newName: "IX_portfolios_accountId");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "accounts",
                newName: "userId");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "accounts",
                newName: "updatedAt");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "accounts",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "Currency",
                table: "accounts",
                newName: "currency");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "accounts",
                newName: "createdAt");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "accounts",
                newName: "id");

            migrationBuilder.RenameIndex(
                name: "IX_accounts_UserId",
                table: "accounts",
                newName: "IX_accounts_userId");

            migrationBuilder.CreateTable(
                name: "dividends",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    symbol = table.Column<string>(type: "text", nullable: false),
                    amount = table.Column<double>(type: "double precision", nullable: false),
                    quantity = table.Column<double>(type: "double precision", nullable: true),
                    perShareAmount = table.Column<double>(type: "double precision", nullable: true),
                    type = table.Column<string>(type: "text", nullable: false),
                    currency = table.Column<string>(type: "text", nullable: false),
                    paymentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    exDividendDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    recordDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    taxWithheld = table.Column<double>(type: "double precision", nullable: false),
                    accountId = table.Column<string>(type: "text", nullable: false),
                    userId = table.Column<string>(type: "text", nullable: false),
                    createdAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dividends", x => x.id);
                    table.ForeignKey(
                        name: "FK_dividends_accounts_accountId",
                        column: x => x.accountId,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_dividends_users_userId",
                        column: x => x.userId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dividends_accountId",
                table: "dividends",
                column: "accountId");

            migrationBuilder.CreateIndex(
                name: "IX_dividends_userId",
                table: "dividends",
                column: "userId");

            migrationBuilder.AddForeignKey(
                name: "FK_accounts_users_userId",
                table: "accounts",
                column: "userId",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_portfolios_accounts_accountId",
                table: "portfolios",
                column: "accountId",
                principalTable: "accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_portfolios_users_userId",
                table: "portfolios",
                column: "userId",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_trades_accounts_accountId",
                table: "trades",
                column: "accountId",
                principalTable: "accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_trades_users_userId",
                table: "trades",
                column: "userId",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_accounts_users_userId",
                table: "accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_portfolios_accounts_accountId",
                table: "portfolios");

            migrationBuilder.DropForeignKey(
                name: "FK_portfolios_users_userId",
                table: "portfolios");

            migrationBuilder.DropForeignKey(
                name: "FK_trades_accounts_accountId",
                table: "trades");

            migrationBuilder.DropForeignKey(
                name: "FK_trades_users_userId",
                table: "trades");

            migrationBuilder.DropTable(
                name: "dividends");

            migrationBuilder.RenameColumn(
                name: "updatedAt",
                table: "users",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "password",
                table: "users",
                newName: "Password");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "users",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "email",
                table: "users",
                newName: "Email");

            migrationBuilder.RenameColumn(
                name: "createdAt",
                table: "users",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "users",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_users_email",
                table: "users",
                newName: "IX_users_Email");

            migrationBuilder.RenameColumn(
                name: "userId",
                table: "trades",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "updatedAt",
                table: "trades",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "type",
                table: "trades",
                newName: "Type");

            migrationBuilder.RenameColumn(
                name: "symbol",
                table: "trades",
                newName: "Symbol");

            migrationBuilder.RenameColumn(
                name: "quantity",
                table: "trades",
                newName: "Quantity");

            migrationBuilder.RenameColumn(
                name: "price",
                table: "trades",
                newName: "Price");

            migrationBuilder.RenameColumn(
                name: "notes",
                table: "trades",
                newName: "Notes");

            migrationBuilder.RenameColumn(
                name: "fee",
                table: "trades",
                newName: "Fee");

            migrationBuilder.RenameColumn(
                name: "date",
                table: "trades",
                newName: "Date");

            migrationBuilder.RenameColumn(
                name: "currency",
                table: "trades",
                newName: "Currency");

            migrationBuilder.RenameColumn(
                name: "createdAt",
                table: "trades",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "accountId",
                table: "trades",
                newName: "AccountId");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "trades",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_trades_userId",
                table: "trades",
                newName: "IX_trades_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_trades_accountId",
                table: "trades",
                newName: "IX_trades_AccountId");

            migrationBuilder.RenameColumn(
                name: "userId",
                table: "portfolios",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "updatedAt",
                table: "portfolios",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "symbol",
                table: "portfolios",
                newName: "Symbol");

            migrationBuilder.RenameColumn(
                name: "quantity",
                table: "portfolios",
                newName: "Quantity");

            migrationBuilder.RenameColumn(
                name: "currentPrice",
                table: "portfolios",
                newName: "CurrentPrice");

            migrationBuilder.RenameColumn(
                name: "averagePrice",
                table: "portfolios",
                newName: "AveragePrice");

            migrationBuilder.RenameColumn(
                name: "accountId",
                table: "portfolios",
                newName: "AccountId");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "portfolios",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_portfolios_userId_accountId_symbol",
                table: "portfolios",
                newName: "IX_portfolios_UserId_AccountId_Symbol");

            migrationBuilder.RenameIndex(
                name: "IX_portfolios_accountId",
                table: "portfolios",
                newName: "IX_portfolios_AccountId");

            migrationBuilder.RenameColumn(
                name: "userId",
                table: "accounts",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "updatedAt",
                table: "accounts",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "accounts",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "currency",
                table: "accounts",
                newName: "Currency");

            migrationBuilder.RenameColumn(
                name: "createdAt",
                table: "accounts",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "accounts",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_accounts_userId",
                table: "accounts",
                newName: "IX_accounts_UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_accounts_users_UserId",
                table: "accounts",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_portfolios_accounts_AccountId",
                table: "portfolios",
                column: "AccountId",
                principalTable: "accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_portfolios_users_UserId",
                table: "portfolios",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_trades_accounts_AccountId",
                table: "trades",
                column: "AccountId",
                principalTable: "accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_trades_users_UserId",
                table: "trades",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
