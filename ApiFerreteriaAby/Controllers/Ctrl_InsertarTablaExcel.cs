using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class Ctrl_InsertarTablaExcel : ControllerBase
{
    private readonly string _connectionString;

    public Ctrl_InsertarTablaExcel(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("conexion");
    }

    [HttpPost]
    public IActionResult Post([FromForm] ExcelDataRequest request)
    {
        try
        {
            using (var package = new ExcelPackage(request.ExcelFile.OpenReadStream()))
            {
                ExcelWorksheet worksheet = package.Workbook.Worksheets[request.ExcelSheetName];

                var rowCount = worksheet.Dimension.Rows;
                var colCount = worksheet.Dimension.Columns;

                var tableColumns = GetTableColumns(request.SqlTableName);

                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();

                    for (int row = 2; row <= rowCount; row++)
                    {
                        var firstCell = worksheet.Cells[row, 1].Value;
                        if (firstCell != null && !string.IsNullOrWhiteSpace(firstCell.ToString()))
                        {
                            var parameters = new DynamicParameters();

                            for (int col = 1; col <= colCount; col++)
                            {
                                var columnName = worksheet.Cells[1, col].Value?.ToString();
                                var column = tableColumns.FirstOrDefault(c => c.ColumnName == columnName);
                                if (column != null)
                                {
                                    var value = worksheet.Cells[row, col].Value;
                                    parameters.Add(column.ColumnName, value);
                                }
                            }

                            var sqlColumns = string.Join(", ", parameters.ParameterNames);
                            var sqlValues = string.Join(", ", parameters.ParameterNames.Select(p => "@" + p));

                            var sql = $"INSERT INTO {request.SqlTableName} ({sqlColumns}) VALUES ({sqlValues})";

                            connection.Execute(sql, parameters);
                        }
                    }

                    connection.Close();
                }
            }

            return Ok("Datos insertados correctamente");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error al insertar datos: {ex.Message}");
        }
    }

    private List<ColumnSchema> GetTableColumns(string tableName)
    {
        var columns = new List<ColumnSchema>();
        var query = $"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}'";

        using (var connection = new SqlConnection(_connectionString))
        {
            connection.Open();

            using (var command = new SqlCommand(query, connection))
            {
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        columns.Add(new ColumnSchema { ColumnName = reader.GetString(0) });
                    }
                }
            }

            connection.Close();
        }

        return columns;
    }
}

public class ExcelDataRequest
{
    public IFormFile ExcelFile { get; set; }
    public string ExcelSheetName { get; set; }
    public string SqlTableName { get; set; }
}

public class ColumnSchema
{
    public string ColumnName { get; set; }
}
