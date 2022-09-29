using LBPUnion.ProjectLighthouse;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ProjectLighthouse.Migrations
{
    [DbContext(typeof(Database))]
    [Migration("20220927000200_VitaCustomRewards")]
    public partial class VitaCustomRewards : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VitaRewardData",
                columns: table => new
                {
                    SlotId = table.Column<int>(type: "int", nullable: false),
                    R1Enabled = table.Column<bool>(type: "bool", nullable: false, defaultValue: null),
                    R1Description = table.Column<string>(type: "longtext", nullable: false),
                    R1Condition = table.Column<string>(type: "longtext", nullable: false),
                    R1AmountNeeded = table.Column<float>(type: "float", nullable: false),
                    R2Enabled = table.Column<bool>(type: "bool", nullable: false, defaultValue: null),
                    R2Description = table.Column<string>(type: "longtext", nullable: false),
                    R2Condition = table.Column<string>(type: "longtext", nullable: false),
                    R2AmountNeeded = table.Column<float>(type: "float", nullable: false),
                    R3Enabled = table.Column<bool>(type: "bool", nullable: false, defaultValue: null),
                    R3Description = table.Column<string>(type: "longtext", nullable: false),
                    R3Condition = table.Column<string>(type: "longtext", nullable: false),
                    R3AmountNeeded = table.Column<float>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VitaRewardData", x => x.SlotId);
                    table.ForeignKey(
                        name: "FK_VitaRewardData_Slots_SlotId",
                        column: x=> x.SlotId,
                        principalTable: "Slots",
                        principalColumn: "SlotId",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "VitaRewardData");
        }
    }
}