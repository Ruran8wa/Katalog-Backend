using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Katalog_Backend.Migrations;

public partial class AddOutboxSessionId : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "SessionId",
            table: "OutboxMessages",
            type: "nvarchar(max)",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "SessionId",
            table: "OutboxMessages");
    }
}
