﻿using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IT15.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaveBalanceToUserProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LeaveBalance",
                table: "UserProfiles",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LeaveBalance",
                table: "UserProfiles");
        }
    }
}
