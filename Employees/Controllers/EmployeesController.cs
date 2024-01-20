using Dapper;
using Employees.Models;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Text;

namespace Employees.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class EmployeesController : ControllerBase
    {
        private readonly IConfiguration _config;
        public EmployeesController(IConfiguration config)
        {
            _config = config;
        }

        [HttpPost]
        public async Task<ActionResult<int>> AddEmployee([FromBody] Employee employee)
        {
            using (var dbConnection = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                await dbConnection.OpenAsync();

                // Проверяем существование паспорта в таблице Passports
                var existingPassport = dbConnection.QuerySingleOrDefault<Passport>("SELECT * FROM Passports WHERE Type = @Type AND Number = @Number", employee.Passport);

                // Если паспорта нет, добавляем его
                if (existingPassport == null)
                {
                    var passportSqlQuery = "INSERT INTO Passports (Type, Number) VALUES (@Type, @Number); SELECT CAST(SCOPE_IDENTITY() as int)";
                    employee.Passport.Id = dbConnection.Query<int>(passportSqlQuery, employee.Passport).Single();
                }
                else
                {
                    employee.Passport.Id = existingPassport.Id;
                }

                // Проверяем существование отдела в таблице Departments
                var existingDepartment = dbConnection.QuerySingleOrDefault<Department>("SELECT * FROM Departments WHERE Name = @Name", employee.Department);

                // Если отдела нет, добавляем его
                if (existingDepartment == null)
                {
                    var departmentSqlQuery = "INSERT INTO Departments (Name, Phone) VALUES (@Name, @Phone); SELECT CAST(SCOPE_IDENTITY() as int)";
                    employee.Department.Id = dbConnection.Query<int>(departmentSqlQuery, employee.Department).Single();
                }
                else
                {
                    employee.Department.Id = existingDepartment.Id;
                }

                var employeeSqlQuery = "INSERT INTO Employees (Name, Surname, Phone, CompanyId, PassportId, DepartmentId) " +
                                       "VALUES (@Name, @Surname, @Phone, @CompanyId, @PassportId, @DepartmentId); " +
                                       "SELECT CAST(SCOPE_IDENTITY() as int)";

                var employeeId = dbConnection.Query<int>(employeeSqlQuery, new
                {
                    Name = employee.Name,
                    Surname = employee.Surname,
                    Phone = employee.Phone,
                    CompanyId = employee.CompanyId,
                    PassportId = employee.Passport.Id,
                    DepartmentId = employee.Department.Id
                }).Single();

                return Ok(new { Id = employeeId });
            }
        }

        [HttpDelete("{id}")]
        public IActionResult DeleteEmployee(int id)
        {
            using (IDbConnection dbConnection = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                var existingEmployee = dbConnection.QuerySingleOrDefault<Employee>("SELECT * FROM Employees WHERE Id = @Id", new { Id = id });

                if (existingEmployee == null)
                {
                    return NotFound("Employee not found.");
                }

                var deleteQuery = "DELETE FROM Employees WHERE Id = @Id";
                dbConnection.Execute(deleteQuery, new { Id = id });

                return NoContent();
            }
        }

        [HttpGet("company/{companyId}")]
        public IActionResult GetEmployeesByCompany(int companyId)
        {
            using (IDbConnection dbConnection = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                var sqlQuery = @"
            SELECT 
                Employees.*,
                Passports.*,
                Departments.*
            FROM Employees
                LEFT JOIN Passports ON Employees.PassportId = Passports.Id
                LEFT JOIN Departments ON Employees.DepartmentId = Departments.Id
            WHERE Employees.CompanyId = @CompanyId";

                var employees = dbConnection.Query<Employee, Passport, Department, Employee>(
                    sqlQuery,
                    (employee, passport, department) =>
                    {
                        employee.Passport = passport;
                        employee.Department = department;
                        return employee;
                    },
                    new { CompanyId = companyId },
                    splitOn: "PassportId, DepartmentId"
                );

                if (employees == null)
                {
                    return NotFound("Employees not found.");
                }

                return Ok(employees);
            }
        }

        [HttpPatch("{id}")]
        public async Task<IActionResult> PatchEmployee(int id, [FromBody] JsonPatchDocument<Employee> patchDoc)
        {
            using (IDbConnection dbConnection = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                if (patchDoc == null)
                {
                    return BadRequest();
                }

                var sqlQuery = @"
                    SELECT 
                        Employees.*,
                        Passports.*,
                        Departments.*
                    FROM Employees
                    LEFT JOIN Passports ON Employees.PassportId = Passports.Id
                    LEFT JOIN Departments ON Employees.DepartmentId = Departments.Id
                    WHERE Employees.Id = @Id";

                var employee = dbConnection.Query<Employee, Passport, Department, Employee>(
                    sqlQuery,
                    (employee, passport, department) =>
                    {
                        employee.Passport = passport;
                        employee.Department = department;
                        return employee;
                    },
                    new { Id = id },
                    splitOn: "Id,Id"
                ).FirstOrDefault();

                if (employee == null)
                {
                    return NotFound();
                }

                patchDoc.ApplyTo(employee);

                await dbConnection.ExecuteAsync(@"
                UPDATE Employees
                SET
                    Name = COALESCE(@Name, Name),
                    Surname = COALESCE(@Surname, Surname),
                    Phone = COALESCE(@Phone, Phone)
                WHERE Id = @Id;

                UPDATE Passports
                SET
                    Type = COALESCE(@Passport_Type, Type),
                    Number = COALESCE(@Passport_Number, Number)
                WHERE Id = @PassportId",
                    new
                    {
                        Id = id,
                        Name = employee.Name,
                        Surname = employee.Surname,
                        Phone = employee.Phone,
                        Passport_Type = employee.Passport.Type,
                        Passport_Number = employee.Passport.Number,
                        PassportId = employee.Passport.Id
                    });

                return NoContent();

            }
        }

        [HttpGet("department/{departmentId}")]
        public IActionResult GetEmployeesByDepartment(int departmentId)
        {
            using (IDbConnection dbConnection = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
            {
                var sqlQuery = @"
            SELECT 
                Employees.*,
                Passports.*,
                Departments.*
            FROM Employees
            LEFT JOIN Passports ON Employees.PassportId = Passports.Id
            LEFT JOIN Departments ON Employees.DepartmentId = Departments.Id
            WHERE Employees.DepartmentId = @DepartmentId";

                var employees = dbConnection.Query<Employee, Passport, Department, Employee>(
                    sqlQuery,
                    (employee, passport, department) =>
                    {
                        employee.Passport = passport;
                        employee.Department = department;
                        return employee;
                    },
                    new { DepartmentId = departmentId },
                    splitOn: "PassportId, DepartmentId"
                );

                if (employees == null)
                {
                    return NotFound("Employees not found.");
                }

                return Ok(employees);
            }
        }

    }
}
