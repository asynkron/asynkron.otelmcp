using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asynkron.OtelReceiver.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComponentMetaData");

            migrationBuilder.DropTable(
                name: "Snapshots");

            migrationBuilder.DropTable(
                name: "UserSettings");

            migrationBuilder.AlterColumn<string>(
                name: "TraceId",
                table: "Spans",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<long>(
                name: "StartTimestamp",
                table: "Spans",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<string>(
                name: "ServiceName",
                table: "Spans",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<byte[]>(
                name: "Proto",
                table: "Spans",
                type: "BLOB",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "bytea");

            migrationBuilder.AlterColumn<string>(
                name: "ParentSpanId",
                table: "Spans",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "OperationName",
                table: "Spans",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Events",
                table: "Spans",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text[]");

            migrationBuilder.AlterColumn<long>(
                name: "EndTimestamp",
                table: "Spans",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<string>(
                name: "AttributeMap",
                table: "Spans",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text[]");

            migrationBuilder.AlterColumn<string>(
                name: "SpanId",
                table: "Spans",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "SpanNames",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ServiceName",
                table: "SpanNames",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Value",
                table: "SpanAttributeValues",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "SpanId",
                table: "SpanAttributeValues",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<byte>(
                name: "Source",
                table: "SpanAttributeValues",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(byte),
                oldType: "smallint");

            migrationBuilder.AlterColumn<string>(
                name: "Key",
                table: "SpanAttributeValues",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "SpanAttributeValues",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Sqlite:Autoincrement", true)
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.AlterColumn<string>(
                name: "Value",
                table: "SpanAttributes",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Key",
                table: "SpanAttributes",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Unit",
                table: "Metrics",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<ulong>(
                name: "StartTimestamp",
                table: "Metrics",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,0)");

            migrationBuilder.AlterColumn<byte[]>(
                name: "Proto",
                table: "Metrics",
                type: "BLOB",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "bytea");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Metrics",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<ulong>(
                name: "EndTimestamp",
                table: "Metrics",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,0)");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Metrics",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "AttributeMap",
                table: "Metrics",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text[]");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Metrics",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Sqlite:Autoincrement", true)
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.AlterColumn<string>(
                name: "TraceId",
                table: "Logs",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<long>(
                name: "Timestamp",
                table: "Logs",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<string>(
                name: "SpanId",
                table: "Logs",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "SeverityText",
                table: "Logs",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<byte>(
                name: "SeverityNumber",
                table: "Logs",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(byte),
                oldType: "smallint");

            migrationBuilder.AlterColumn<string>(
                name: "RawBody",
                table: "Logs",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<byte[]>(
                name: "Proto",
                table: "Logs",
                type: "BLOB",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "bytea");

            migrationBuilder.AlterColumn<long>(
                name: "ObservedTimestamp",
                table: "Logs",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<string>(
                name: "Body",
                table: "Logs",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Logs",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Sqlite:Autoincrement", true)
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.AlterColumn<string>(
                name: "Value",
                table: "LogAttributes",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<byte>(
                name: "Source",
                table: "LogAttributes",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(byte),
                oldType: "smallint");

            migrationBuilder.AlterColumn<int>(
                name: "LogId",
                table: "LogAttributes",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Key",
                table: "LogAttributes",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "LogAttributes",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Sqlite:Autoincrement", true)
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.CreateIndex(
                name: "IX_Spans_ServiceName_EndTimestamp",
                table: "Spans",
                columns: new[] { "ServiceName", "EndTimestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_Spans_ServiceName_StartTimestamp",
                table: "Spans",
                columns: new[] { "ServiceName", "StartTimestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_Spans_TraceId_StartTimestamp",
                table: "Spans",
                columns: new[] { "TraceId", "StartTimestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_SpanAttributeValues_SpanId_Key_Value",
                table: "SpanAttributeValues",
                columns: new[] { "SpanId", "Key", "Value" });

            migrationBuilder.CreateIndex(
                name: "IX_LogAttributes_LogId_Key_Value",
                table: "LogAttributes",
                columns: new[] { "LogId", "Key", "Value" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Spans_ServiceName_EndTimestamp",
                table: "Spans");

            migrationBuilder.DropIndex(
                name: "IX_Spans_ServiceName_StartTimestamp",
                table: "Spans");

            migrationBuilder.DropIndex(
                name: "IX_Spans_TraceId_StartTimestamp",
                table: "Spans");

            migrationBuilder.DropIndex(
                name: "IX_SpanAttributeValues_SpanId_Key_Value",
                table: "SpanAttributeValues");

            migrationBuilder.DropIndex(
                name: "IX_LogAttributes_LogId_Key_Value",
                table: "LogAttributes");

            migrationBuilder.AlterColumn<string>(
                name: "TraceId",
                table: "Spans",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<long>(
                name: "StartTimestamp",
                table: "Spans",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<string>(
                name: "ServiceName",
                table: "Spans",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<byte[]>(
                name: "Proto",
                table: "Spans",
                type: "bytea",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "BLOB");

            migrationBuilder.AlterColumn<string>(
                name: "ParentSpanId",
                table: "Spans",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "OperationName",
                table: "Spans",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "Events",
                table: "Spans",
                type: "text[]",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<long>(
                name: "EndTimestamp",
                table: "Spans",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<string>(
                name: "AttributeMap",
                table: "Spans",
                type: "text[]",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "SpanId",
                table: "Spans",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "SpanNames",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "ServiceName",
                table: "SpanNames",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "Value",
                table: "SpanAttributeValues",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "SpanId",
                table: "SpanAttributeValues",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<byte>(
                name: "Source",
                table: "SpanAttributeValues",
                type: "smallint",
                nullable: false,
                oldClrType: typeof(byte),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<string>(
                name: "Key",
                table: "SpanAttributeValues",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "SpanAttributeValues",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("Sqlite:Autoincrement", true)
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.AlterColumn<string>(
                name: "Value",
                table: "SpanAttributes",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "Key",
                table: "SpanAttributes",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "Unit",
                table: "Metrics",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<decimal>(
                name: "StartTimestamp",
                table: "Metrics",
                type: "numeric(20,0)",
                nullable: false,
                oldClrType: typeof(ulong),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<byte[]>(
                name: "Proto",
                table: "Metrics",
                type: "bytea",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "BLOB");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Metrics",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<decimal>(
                name: "EndTimestamp",
                table: "Metrics",
                type: "numeric(20,0)",
                nullable: false,
                oldClrType: typeof(ulong),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Metrics",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "AttributeMap",
                table: "Metrics",
                type: "text[]",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Metrics",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("Sqlite:Autoincrement", true)
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.AlterColumn<string>(
                name: "TraceId",
                table: "Logs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<long>(
                name: "Timestamp",
                table: "Logs",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<string>(
                name: "SpanId",
                table: "Logs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "SeverityText",
                table: "Logs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<byte>(
                name: "SeverityNumber",
                table: "Logs",
                type: "smallint",
                nullable: false,
                oldClrType: typeof(byte),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<string>(
                name: "RawBody",
                table: "Logs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<byte[]>(
                name: "Proto",
                table: "Logs",
                type: "bytea",
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "BLOB");

            migrationBuilder.AlterColumn<long>(
                name: "ObservedTimestamp",
                table: "Logs",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<string>(
                name: "Body",
                table: "Logs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Logs",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("Sqlite:Autoincrement", true)
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.AlterColumn<string>(
                name: "Value",
                table: "LogAttributes",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<byte>(
                name: "Source",
                table: "LogAttributes",
                type: "smallint",
                nullable: false,
                oldClrType: typeof(byte),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "LogId",
                table: "LogAttributes",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<string>(
                name: "Key",
                table: "LogAttributes",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "LogAttributes",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("Sqlite:Autoincrement", true)
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.CreateTable(
                name: "ComponentMetaData",
                columns: table => new
                {
                    NamePath = table.Column<string>(type: "text", nullable: false),
                    Annotation = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComponentMetaData", x => x.NamePath);
                });

            migrationBuilder.CreateTable(
                name: "Snapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Proto = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Snapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Theme = table.Column<string>(type: "text", nullable: false),
                    TimestampType = table.Column<byte>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSettings", x => x.Id);
                });
        }
    }
}
