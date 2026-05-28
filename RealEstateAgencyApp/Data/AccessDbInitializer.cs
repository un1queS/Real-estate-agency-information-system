using System.Data.OleDb;
using System.IO;
using System.Data;

namespace RealEstateAgencyApp.Data;

public static class AccessDbInitializer
{
    public static async Task EnsureDatabaseAsync(string databasePath)
    {
        bool isNewDatabase = false;
        if (!File.Exists(databasePath))
        {
            await CreateAccessFileAsync(databasePath);
            isNewDatabase = true;
        }

        // Для существующей пользовательской БД не создаем таблицы автоматически.
        // Это защищает рабочую схему от лишних системных таблиц приложения.
        if (isNewDatabase)
        {
            string connectionString = BuildConnectionString(databasePath);
            await EnsureSchemaAsync(connectionString);
        }
    }

    public static string BuildConnectionString(string databasePath) =>
        $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={databasePath};Persist Security Info=False;";

    private static Task CreateAccessFileAsync(string databasePath)
    {
        return Task.Run(() =>
        {
            try
            {
                // Создаем файл Access через COM-объект ADOX Catalog.
                Type? catalogType = Type.GetTypeFromProgID("ADOX.Catalog");
                if (catalogType is null)
                {
                    throw new InvalidOperationException("ADOX.Catalog не найден. Убедитесь, что установлен Microsoft Access Database Engine.");
                }

                dynamic catalog = Activator.CreateInstance(catalogType)
                    ?? throw new InvalidOperationException("Не удалось создать ADOX.Catalog.");

                try
                {
                    string createConnection = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={databasePath};Jet OLEDB:Engine Type=5;";
                    catalog.Create(createConnection);
                }
                finally
                {
                    try
                    {
                        System.Runtime.InteropServices.Marshal.FinalReleaseComObject(catalog);
                    }
                    catch
                    {
                        // Игнорируем ошибку освобождения COM.
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка создания файла БД '{databasePath}': {ex.Message}", ex);
            }
        });
    }

    private static async Task EnsureSchemaAsync(string connectionString)
    {
        var tables = new (string Name, string Sql)[]
        {
            ("Clients", @"CREATE TABLE Clients (
                ClientID COUNTER PRIMARY KEY,
                FullName TEXT(255) NOT NULL,
                Phone TEXT(50),
                Email TEXT(255),
                ClientType TEXT(50) NOT NULL
            )"),
            ("Properties", @"CREATE TABLE Properties (
                PropertyID COUNTER PRIMARY KEY,
                [Address] TEXT(255) NOT NULL,
                PropertyType TEXT(50) NOT NULL,
                Area DOUBLE,
                Rooms INTEGER,
                Floor INTEGER,
                Price CURRENCY,
                [Status] TEXT(50) NOT NULL,
                OwnerID INTEGER NOT NULL
            )"),
            ("Deals", @"CREATE TABLE Deals (
                DealID COUNTER PRIMARY KEY,
                PropertyID INTEGER NOT NULL,
                ClientID INTEGER NOT NULL,
                DealType TEXT(50) NOT NULL,
                Amount CURRENCY,
                DealDate DATETIME,
                [Status] TEXT(50) NOT NULL
            )"),
            ("Users", @"CREATE TABLE Users (
                UserID COUNTER PRIMARY KEY,
                [Login] TEXT(100) NOT NULL,
                [Password] TEXT(100) NOT NULL
            )")
        };

        using var connection = new OleDbConnection(connectionString);
        await connection.OpenAsync();

        foreach ((string tableName, string ddl) in tables)
        {
            // Проверяем существование таблицы через метаданные Access,
            // чтобы не зависеть от языка текста ошибки драйвера.
            if (TableExists(connection, tableName))
            {
                continue;
            }

            using var command = new OleDbCommand(ddl, connection);
            await command.ExecuteNonQueryAsync();
        }

        if (TableExists(connection, "Users"))
        {
            await EnsureDefaultManagerAsync(connection);
        }
    }

    private static bool TableExists(OleDbConnection connection, string tableName)
    {
        DataTable schema = connection.GetSchema(
            "Tables",
            new[] { null, null, tableName, "TABLE" });

        return schema.Rows.Count > 0;
    }

    private static async Task EnsureDefaultManagerAsync(OleDbConnection connection)
    {
        const string checkSql = "SELECT COUNT(*) FROM Users";
        using var checkCmd = new OleDbCommand(checkSql, connection);
        object? countObj = await checkCmd.ExecuteScalarAsync();
        int count = Convert.ToInt32(countObj);

        if (count > 0)
        {
            return;
        }

        // Демо-аккаунт менеджера для первого входа.
        const string insertSql = "INSERT INTO Users ([Login], [Password]) VALUES (?, ?)";
        using var insertCmd = new OleDbCommand(insertSql, connection);
        insertCmd.Parameters.AddWithValue("@login", "manager");
        insertCmd.Parameters.AddWithValue("@password", "12345");
        await insertCmd.ExecuteNonQueryAsync();
    }

}
