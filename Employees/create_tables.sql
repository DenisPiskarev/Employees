CREATE TABLE Departments (
    Id INT PRIMARY KEY IDENTITY(1,1),
    Name NVARCHAR(255),
    Phone NVARCHAR(255)
);
CREATE TABLE Passports (
    Id INT PRIMARY KEY IDENTITY(1,1),
    Type NVARCHAR(255),
    Number NVARCHAR(255)
);
CREATE TABLE Employees (
    Id INT PRIMARY KEY IDENTITY(1,1),
    Name NVARCHAR(255),
    Surname NVARCHAR(255),
    Phone NVARCHAR(255),
    CompanyId INT,
    PassportId INT FOREIGN KEY REFERENCES Passports(Id),
    DepartmentId INT FOREIGN KEY REFERENCES Departments(Id)
);