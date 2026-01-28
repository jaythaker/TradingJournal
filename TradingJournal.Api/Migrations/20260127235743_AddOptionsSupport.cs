using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingJournal.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOptionsSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "contractMultiplier",
                table: "trades",
                type: "integer",
                nullable: false,
                defaultValue: 100);

            migrationBuilder.AddColumn<DateTime>(
                name: "expirationDate",
                table: "trades",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "instrumentType",
                table: "trades",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "isOpeningTrade",
                table: "trades",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "optionType",
                table: "trades",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "spreadGroupId",
                table: "trades",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "spreadLegNumber",
                table: "trades",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "spreadType",
                table: "trades",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "strikePrice",
                table: "trades",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "underlyingSymbol",
                table: "trades",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "contractMultiplier",
                table: "trades");

            migrationBuilder.DropColumn(
                name: "expirationDate",
                table: "trades");

            migrationBuilder.DropColumn(
                name: "instrumentType",
                table: "trades");

            migrationBuilder.DropColumn(
                name: "isOpeningTrade",
                table: "trades");

            migrationBuilder.DropColumn(
                name: "optionType",
                table: "trades");

            migrationBuilder.DropColumn(
                name: "spreadGroupId",
                table: "trades");

            migrationBuilder.DropColumn(
                name: "spreadLegNumber",
                table: "trades");

            migrationBuilder.DropColumn(
                name: "spreadType",
                table: "trades");

            migrationBuilder.DropColumn(
                name: "strikePrice",
                table: "trades");

            migrationBuilder.DropColumn(
                name: "underlyingSymbol",
                table: "trades");
        }
    }
}
