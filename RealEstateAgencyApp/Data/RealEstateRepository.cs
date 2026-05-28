using System.Data;
using System.Data.OleDb;
using RealEstateAgencyApp.Models;

namespace RealEstateAgencyApp.Data;

public sealed class RealEstateRepository
{
    private readonly string _connectionString;
    private SchemaMode _schemaMode = SchemaMode.Unknown;

    private enum SchemaMode
    {
        Unknown = 0,
        Standard = 1,
        Russian = 2
    }

    public RealEstateRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<bool> AuthenticateAsync(string login, string password)
    {
        const string sql = "SELECT COUNT(*) FROM Users WHERE [Login] = ? AND [Password] = ?";
        using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync();
        SchemaMode mode = ResolveSchemaMode(connection);
        if (mode == SchemaMode.Russian)
        {
            // В пользовательской схеме отдельной авторизации может не быть.
            // Сохраняем совместимость с демо-учеткой.
            return login == "manager" && password == "12345";
        }

        using var command = new OleDbCommand(sql, connection);
        command.Parameters.AddWithValue("@login", login);
        command.Parameters.AddWithValue("@password", password);
        object? value = await command.ExecuteScalarAsync();
        return Convert.ToInt32(value) > 0;
    }

    public async Task<List<Client>> GetClientsAsync()
    {
        using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync();
        if (ResolveSchemaMode(connection) == SchemaMode.Russian)
        {
            const string sql = @"SELECT ID_Клиента, Фамилия, Имя, Отчество, Телефон, Email, Тип
                                 FROM Клиенты ORDER BY ID_Клиента";
            var result = new List<Client>();
            using var command = new OleDbCommand(sql, connection);
            using var reader = await command.ExecuteReaderAsync();
            while (reader is not null && await reader.ReadAsync())
            {
                string lastName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                string firstName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                string middleName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
                string fullName = string.Join(" ", new[] { lastName, firstName, middleName }.Where(s => !string.IsNullOrWhiteSpace(s)));

                result.Add(new Client
                {
                    ClientID = Convert.ToInt32(reader.GetValue(0)),
                    FullName = fullName,
                    Phone = reader.IsDBNull(4) ? string.Empty : Convert.ToString(reader.GetValue(4)) ?? string.Empty,
                    Email = reader.IsDBNull(5) ? string.Empty : Convert.ToString(reader.GetValue(5)) ?? string.Empty,
                    ClientType = reader.IsDBNull(6) ? string.Empty : Convert.ToString(reader.GetValue(6)) ?? string.Empty
                });
            }
            return result;
        }

        const string standardSql = "SELECT ClientID, FullName, Phone, Email, ClientType FROM Clients ORDER BY ClientID";
        var standardResult = new List<Client>();
        using (var command = new OleDbCommand(standardSql, connection))
        using (var reader = await command.ExecuteReaderAsync())
        {
            while (reader is not null && await reader.ReadAsync())
            {
                standardResult.Add(new Client
                {
                    ClientID = reader.GetInt32(0),
                    FullName = reader.GetString(1),
                    Phone = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    Email = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    ClientType = reader.IsDBNull(4) ? string.Empty : reader.GetString(4)
                });
            }
        }
        return standardResult;
    }

    public async Task AddClientAsync(Client client)
    {
        using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync();
        if (ResolveSchemaMode(connection) == SchemaMode.Russian)
        {
            ParseFullName(client.FullName, out string lastName, out string firstName, out string middleName);
            const string sql = @"INSERT INTO Клиенты (Фамилия, Имя, Отчество, Тип, Телефон, Email, ПаспортныеДанные, АдресРегистрации)
                                 VALUES (?, ?, ?, ?, ?, ?, ?, ?)";
            using var command = new OleDbCommand(sql, connection);
            command.Parameters.AddWithValue("@lastName", lastName);
            command.Parameters.AddWithValue("@firstName", firstName);
            command.Parameters.AddWithValue("@middleName", middleName);
            command.Parameters.AddWithValue("@type", client.ClientType);
            command.Parameters.AddWithValue("@phone", client.Phone);
            command.Parameters.AddWithValue("@email", client.Email);
            command.Parameters.AddWithValue("@passport", "");
            command.Parameters.AddWithValue("@address", "");
            await command.ExecuteNonQueryAsync();
            return;
        }

        const string standardSql = "INSERT INTO Clients (FullName, Phone, Email, ClientType) VALUES (?, ?, ?, ?)";
        using var standardCommand = new OleDbCommand(standardSql, connection);
        standardCommand.Parameters.AddWithValue("@fullName", client.FullName);
        standardCommand.Parameters.AddWithValue("@phone", client.Phone);
        standardCommand.Parameters.AddWithValue("@email", client.Email);
        standardCommand.Parameters.AddWithValue("@type", client.ClientType);
        await standardCommand.ExecuteNonQueryAsync();
    }

    public async Task UpdateClientAsync(Client client)
    {
        using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync();
        if (ResolveSchemaMode(connection) == SchemaMode.Russian)
        {
            ParseFullName(client.FullName, out string lastName, out string firstName, out string middleName);
            const string sql = @"UPDATE Клиенты SET Фамилия = ?, Имя = ?, Отчество = ?, Тип = ?, Телефон = ?, Email = ?
                                 WHERE ID_Клиента = ?";
            using var command = new OleDbCommand(sql, connection);
            command.Parameters.AddWithValue("@lastName", lastName);
            command.Parameters.AddWithValue("@firstName", firstName);
            command.Parameters.AddWithValue("@middleName", middleName);
            command.Parameters.AddWithValue("@type", client.ClientType);
            command.Parameters.AddWithValue("@phone", client.Phone);
            command.Parameters.AddWithValue("@email", client.Email);
            command.Parameters.AddWithValue("@id", client.ClientID);
            await command.ExecuteNonQueryAsync();
            return;
        }

        const string standardSql = "UPDATE Clients SET FullName = ?, Phone = ?, Email = ?, ClientType = ? WHERE ClientID = ?";
        using var standardCommand = new OleDbCommand(standardSql, connection);
        standardCommand.Parameters.AddWithValue("@fullName", client.FullName);
        standardCommand.Parameters.AddWithValue("@phone", client.Phone);
        standardCommand.Parameters.AddWithValue("@email", client.Email);
        standardCommand.Parameters.AddWithValue("@type", client.ClientType);
        standardCommand.Parameters.AddWithValue("@id", client.ClientID);
        await standardCommand.ExecuteNonQueryAsync();
    }

    public async Task DeleteClientAsync(int clientId)
    {
        using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync();
        if (ResolveSchemaMode(connection) == SchemaMode.Russian)
        {
            const string sql = "DELETE FROM Клиенты WHERE ID_Клиента = ?";
            using var command = new OleDbCommand(sql, connection);
            command.Parameters.AddWithValue("@id", clientId);
            await command.ExecuteNonQueryAsync();
            return;
        }

        const string standardSql = "DELETE FROM Clients WHERE ClientID = ?";
        using var standardCommand = new OleDbCommand(standardSql, connection);
        standardCommand.Parameters.AddWithValue("@id", clientId);
        await standardCommand.ExecuteNonQueryAsync();
    }

    public async Task<List<Property>> GetPropertiesAsync()
    {
        using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync();
        if (ResolveSchemaMode(connection) == SchemaMode.Russian)
        {
            const string sql = @"SELECT ID_Объекта, Адрес, Тип_Объекта, Площадь, Комнат, Этаж, Цена, Статус, Владелец
                                 FROM Объекты_Недвижимости ORDER BY ID_Объекта";
            return await ReadPropertiesAsync(connection, sql, null, russianMode: true);
        }

        const string standardSql = @"SELECT PropertyID, [Address], PropertyType, Area, Rooms, Floor, Price, [Status], OwnerID 
                                     FROM Properties ORDER BY PropertyID";
        return await ReadPropertiesAsync(connection, standardSql, null, russianMode: false);
    }

    public async Task<List<Property>> GetActivePropertiesAsync()
    {
        using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync();
        if (ResolveSchemaMode(connection) == SchemaMode.Russian)
        {
            const string sql = @"SELECT ID_Объекта, Адрес, Тип_Объекта, Площадь, Комнат, Этаж, Цена, Статус, Владелец
                                 FROM Объекты_Недвижимости WHERE Статус IN (?, ?) ORDER BY ID_Объекта";
            return await ReadPropertiesAsync(connection, sql, cmd =>
            {
                cmd.Parameters.AddWithValue("@s1", "Свободно");
                cmd.Parameters.AddWithValue("@s2", "Активно");
            }, russianMode: true);
        }

        const string standardSql = @"SELECT PropertyID, [Address], PropertyType, Area, Rooms, Floor, Price, [Status], OwnerID
                                     FROM Properties WHERE [Status] = ?";
        return await ReadPropertiesAsync(connection, standardSql, cmd => cmd.Parameters.AddWithValue("@status", "Активно"), russianMode: false);
    }

    public async Task<List<Property>> SearchPropertiesAsync(string? type, decimal? minPrice, decimal? maxPrice, int? rooms)
    {
        using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync();
        bool russianMode = ResolveSchemaMode(connection) == SchemaMode.Russian;

        var filters = new List<string>();
        var values = new List<object>();

        if (!string.IsNullOrWhiteSpace(type))
        {
            filters.Add(russianMode ? "Тип_Объекта = ?" : "PropertyType = ?");
            values.Add(type);
        }
        if (minPrice.HasValue)
        {
            filters.Add(russianMode ? "Цена >= ?" : "Price >= ?");
            values.Add(minPrice.Value);
        }
        if (maxPrice.HasValue)
        {
            filters.Add(russianMode ? "Цена <= ?" : "Price <= ?");
            values.Add(maxPrice.Value);
        }
        if (rooms.HasValue)
        {
            filters.Add(russianMode ? "Комнат = ?" : "Rooms = ?");
            values.Add(rooms.Value);
        }

        string where = filters.Count > 0 ? $" WHERE {string.Join(" AND ", filters)}" : string.Empty;
        string sql = russianMode
            ? @"SELECT ID_Объекта, Адрес, Тип_Объекта, Площадь, Комнат, Этаж, Цена, Статус, Владелец
                FROM Объекты_Недвижимости" + where + " ORDER BY ID_Объекта"
            : @"SELECT PropertyID, [Address], PropertyType, Area, Rooms, Floor, Price, [Status], OwnerID
                FROM Properties" + where + " ORDER BY PropertyID";

        return await ReadPropertiesAsync(connection, sql, cmd =>
        {
            foreach (object value in values)
            {
                cmd.Parameters.AddWithValue("@p", value);
            }
        }, russianMode);
    }

    public async Task AddPropertyAsync(Property property)
    {
        using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync();
        if (ResolveSchemaMode(connection) == SchemaMode.Russian)
        {
            const string sql = @"INSERT INTO Объекты_Недвижимости (Владелец, Тип_Объекта, Статус, Адрес, Район, Комнат, Площадь, Этаж, [Кол-во_этажей], Цена, Описание, ДатаДобавления)
                                 VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)";
            using var command = new OleDbCommand(sql, connection);
            command.Parameters.AddWithValue("@owner", property.OwnerID);
            command.Parameters.AddWithValue("@type", property.PropertyType);
            command.Parameters.AddWithValue("@status", NormalizeRussianPropertyStatus(property.Status));
            command.Parameters.AddWithValue("@address", property.Address);
            command.Parameters.AddWithValue("@district", "");
            command.Parameters.AddWithValue("@rooms", property.Rooms);
            command.Parameters.AddWithValue("@area", property.Area);
            command.Parameters.AddWithValue("@floor", property.Floor);
            command.Parameters.AddWithValue("@floors", Math.Max(property.Floor, 1));
            command.Parameters.AddWithValue("@price", property.Price);
            command.Parameters.AddWithValue("@description", "");
            command.Parameters.AddWithValue("@date", DateTime.Now);
            await command.ExecuteNonQueryAsync();
            return;
        }

        const string standardSql = @"INSERT INTO Properties ([Address], PropertyType, Area, Rooms, Floor, Price, [Status], OwnerID)
                                     VALUES (?, ?, ?, ?, ?, ?, ?, ?)";
        using var standardCommand = new OleDbCommand(standardSql, connection);
        FillPropertyParameters(standardCommand, property, includeId: false);
        await standardCommand.ExecuteNonQueryAsync();
    }

    public async Task UpdatePropertyAsync(Property property)
    {
        using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync();
        if (ResolveSchemaMode(connection) == SchemaMode.Russian)
        {
            const string sql = @"UPDATE Объекты_Недвижимости
                                 SET Владелец = ?, Тип_Объекта = ?, Статус = ?, Адрес = ?, Комнат = ?, Площадь = ?, Этаж = ?, Цена = ?
                                 WHERE ID_Объекта = ?";
            using var command = new OleDbCommand(sql, connection);
            command.Parameters.AddWithValue("@owner", property.OwnerID);
            command.Parameters.AddWithValue("@type", property.PropertyType);
            command.Parameters.AddWithValue("@status", NormalizeRussianPropertyStatus(property.Status));
            command.Parameters.AddWithValue("@address", property.Address);
            command.Parameters.AddWithValue("@rooms", property.Rooms);
            command.Parameters.AddWithValue("@area", property.Area);
            command.Parameters.AddWithValue("@floor", property.Floor);
            command.Parameters.AddWithValue("@price", property.Price);
            command.Parameters.AddWithValue("@id", property.PropertyID);
            await command.ExecuteNonQueryAsync();
            return;
        }

        const string standardSql = @"UPDATE Properties 
                                     SET [Address] = ?, PropertyType = ?, Area = ?, Rooms = ?, Floor = ?, Price = ?, [Status] = ?, OwnerID = ?
                                     WHERE PropertyID = ?";
        using var standardCommand = new OleDbCommand(standardSql, connection);
        FillPropertyParameters(standardCommand, property, includeId: true);
        await standardCommand.ExecuteNonQueryAsync();
    }

    public async Task DeletePropertyAsync(int propertyId)
    {
        using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync();
        if (ResolveSchemaMode(connection) == SchemaMode.Russian)
        {
            const string sql = "DELETE FROM Объекты_Недвижимости WHERE ID_Объекта = ?";
            using var command = new OleDbCommand(sql, connection);
            command.Parameters.AddWithValue("@id", propertyId);
            await command.ExecuteNonQueryAsync();
            return;
        }

        const string standardSql = "DELETE FROM Properties WHERE PropertyID = ?";
        using var standardCommand = new OleDbCommand(standardSql, connection);
        standardCommand.Parameters.AddWithValue("@id", propertyId);
        await standardCommand.ExecuteNonQueryAsync();
    }

    public async Task<List<Deal>> GetDealsAsync()
    {
        using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync();
        if (ResolveSchemaMode(connection) == SchemaMode.Russian)
        {
            const string sql = @"SELECT ID_Сделки, Объект, Покупатель, 'Продажа' AS DealType, Цена_Продажи, Дата_Сделки, 'Завершена' AS DealStatus
                                 FROM Сделки ORDER BY Дата_Сделки DESC";
            return await ReadDealsAsync(connection, sql, null);
        }

        const string standardSql = "SELECT DealID, PropertyID, ClientID, DealType, Amount, DealDate, [Status] FROM Deals ORDER BY DealDate DESC";
        return await ReadDealsAsync(connection, standardSql, null);
    }

    public async Task AddDealAsync(Deal deal)
    {
        using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync();
        if (ResolveSchemaMode(connection) == SchemaMode.Russian)
        {
            const string sql = @"INSERT INTO Сделки (Номер_Договора, Дата_Сделки, Объект, Продавец, Покупатель, Риелтор, Цена_Продажи, Комиссия, ДатаРегистрации)
                                 VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)";
            using var command = new OleDbCommand(sql, connection);
            // В русских учебных базах типы столбцов могут отличаться (число/текст/lookup).
            // Подбираем OleDbType по схеме таблицы, чтобы избежать "несоответствия типов данных".
            command.Parameters.Add("@num", OleDbType.VarWChar, 50).Value = $"ДКП-{DateTime.Now:yyyyMMdd-HHmmss}";
            command.Parameters.Add("@date", GetOleDbTypeForColumn(connection, "Сделки", "Дата_Сделки", OleDbType.DBTimeStamp))
                .Value = CoerceValue(GetOleDbTypeForColumn(connection, "Сделки", "Дата_Сделки", OleDbType.DBTimeStamp), deal.DealDate);

            OleDbType objType = GetOleDbTypeForColumn(connection, "Сделки", "Объект", OleDbType.Integer);
            command.Parameters.Add("@object", objType).Value = CoerceValue(objType, deal.PropertyID);

            OleDbType sellerType = GetOleDbTypeForColumn(connection, "Сделки", "Продавец", OleDbType.Integer);
            command.Parameters.Add("@seller", sellerType).Value = CoerceValue(sellerType, deal.ClientID);

            OleDbType buyerType = GetOleDbTypeForColumn(connection, "Сделки", "Покупатель", OleDbType.Integer);
            command.Parameters.Add("@buyer", buyerType).Value = CoerceValue(buyerType, deal.ClientID);

            OleDbType realtorType = GetOleDbTypeForColumn(connection, "Сделки", "Риелтор", OleDbType.Integer);
            command.Parameters.Add("@realtor", realtorType).Value = CoerceValue(realtorType, 1);

            OleDbType amountType = GetOleDbTypeForColumn(connection, "Сделки", "Цена_Продажи", OleDbType.Currency);
            command.Parameters.Add("@amount", amountType).Value = CoerceValue(amountType, deal.Amount);

            OleDbType commissionType = GetOleDbTypeForColumn(connection, "Сделки", "Комиссия", OleDbType.Currency);
            command.Parameters.Add("@commission", commissionType).Value = CoerceValue(commissionType, Math.Round(deal.Amount * 0.05m, 2));

            OleDbType regType = GetOleDbTypeForColumn(connection, "Сделки", "ДатаРегистрации", OleDbType.DBTimeStamp);
            command.Parameters.Add("@regDate", regType).Value = CoerceValue(regType, DateTime.Now);
            await command.ExecuteNonQueryAsync();
            return;
        }

        const string standardSql = "INSERT INTO Deals (PropertyID, ClientID, DealType, Amount, DealDate, [Status]) VALUES (?, ?, ?, ?, ?, ?)";
        using var standardCommand = new OleDbCommand(standardSql, connection);
        FillDealParameters(standardCommand, deal, includeId: false);
        await standardCommand.ExecuteNonQueryAsync();
    }

    public async Task UpdateDealAsync(Deal deal)
    {
        using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync();
        if (ResolveSchemaMode(connection) == SchemaMode.Russian)
        {
            const string sql = @"UPDATE Сделки SET Дата_Сделки = ?, Объект = ?, Покупатель = ?, Продавец = ?, Цена_Продажи = ?, Комиссия = ?
                                 WHERE ID_Сделки = ?";
            using var command = new OleDbCommand(sql, connection);
            command.Parameters.Add("@date", OleDbType.DBTimeStamp).Value = deal.DealDate;
            command.Parameters.Add("@object", OleDbType.Integer).Value = deal.PropertyID;
            command.Parameters.Add("@buyer", OleDbType.Integer).Value = deal.ClientID;
            command.Parameters.Add("@seller", OleDbType.Integer).Value = deal.ClientID;
            command.Parameters.Add("@amount", OleDbType.Currency).Value = deal.Amount;
            command.Parameters.Add("@commission", OleDbType.Currency).Value = Math.Round(deal.Amount * 0.05m, 2);
            command.Parameters.Add("@id", OleDbType.Integer).Value = deal.DealID;
            await command.ExecuteNonQueryAsync();
            return;
        }

        const string standardSql = @"UPDATE Deals SET PropertyID = ?, ClientID = ?, DealType = ?, Amount = ?, DealDate = ?, [Status] = ?
                                     WHERE DealID = ?";
        using var standardCommand = new OleDbCommand(standardSql, connection);
        FillDealParameters(standardCommand, deal, includeId: true);
        await standardCommand.ExecuteNonQueryAsync();
    }

    public async Task DeleteDealAsync(int dealId)
    {
        using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync();
        if (ResolveSchemaMode(connection) == SchemaMode.Russian)
        {
            const string sql = "DELETE FROM Сделки WHERE ID_Сделки = ?";
            using var command = new OleDbCommand(sql, connection);
            command.Parameters.AddWithValue("@id", dealId);
            await command.ExecuteNonQueryAsync();
            return;
        }

        const string standardSql = "DELETE FROM Deals WHERE DealID = ?";
        using var standardCommand = new OleDbCommand(standardSql, connection);
        standardCommand.Parameters.AddWithValue("@id", dealId);
        await standardCommand.ExecuteNonQueryAsync();
    }

    public async Task CreateCompletedDealAsync(int propertyId, int clientId, string dealType, decimal amount)
    {
        using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync();
        if (ResolveSchemaMode(connection) == SchemaMode.Russian)
        {
            using var transactionRu = connection.BeginTransaction();
            try
            {
                const string insertRu = @"INSERT INTO Сделки (Номер_Договора, Дата_Сделки, Объект, Продавец, Покупатель, Риелтор, Цена_Продажи, Комиссия, ДатаРегистрации)
                                          VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)";
                using (var insertCmd = new OleDbCommand(insertRu, connection, transactionRu))
                {
                    insertCmd.Parameters.AddWithValue("@num", $"ДКП-{DateTime.Now:yyyyMMdd-HHmmss}");
                    insertCmd.Parameters.AddWithValue("@date", DateTime.Now);
                    insertCmd.Parameters.AddWithValue("@object", propertyId);
                    insertCmd.Parameters.AddWithValue("@seller", clientId);
                    insertCmd.Parameters.AddWithValue("@buyer", clientId);
                    insertCmd.Parameters.AddWithValue("@realtor", 1);
                    insertCmd.Parameters.AddWithValue("@amount", amount);
                    insertCmd.Parameters.AddWithValue("@commission", Math.Round(amount * 0.05m, 2));
                    insertCmd.Parameters.AddWithValue("@regDate", DateTime.Now);
                    await insertCmd.ExecuteNonQueryAsync();
                }

                const string updateRu = "UPDATE Объекты_Недвижимости SET Статус = ? WHERE ID_Объекта = ?";
                using (var updateCmd = new OleDbCommand(updateRu, connection, transactionRu))
                {
                    string newStatus = dealType == "Аренда" ? "Сдано" : "Продано";
                    updateCmd.CommandText = "UPDATE [Объекты_Недвижимости] SET [Статус] = ? WHERE [ID_Объекта] = ?";
                    updateCmd.Parameters.Add("@status", OleDbType.VarWChar, 50).Value = newStatus;
                    updateCmd.Parameters.Add("@id", OleDbType.Integer).Value = propertyId;
                    await updateCmd.ExecuteNonQueryAsync();
                }

                transactionRu.Commit();
                return;
            }
            catch
            {
                transactionRu.Rollback();
                throw;
            }
        }

        using var transaction = connection.BeginTransaction();
        try
        {
            const string insertDealSql =
                "INSERT INTO Deals (PropertyID, ClientID, DealType, Amount, DealDate, [Status]) VALUES (?, ?, ?, ?, ?, ?)";
            using (var insertCmd = new OleDbCommand(insertDealSql, connection, transaction))
            {
                insertCmd.Parameters.Add("@propertyId", OleDbType.Integer).Value = propertyId;
                insertCmd.Parameters.Add("@clientId", OleDbType.Integer).Value = clientId;
                insertCmd.Parameters.Add("@dealType", OleDbType.VarWChar, 50).Value = dealType;
                insertCmd.Parameters.Add(
                    new OleDbParameter("@amount", OleDbType.Decimal)
                    {
                        Precision = 19,
                        Scale = 4,
                        Value = decimal.Round(amount, 4, MidpointRounding.AwayFromZero)
                    });
                insertCmd.Parameters.Add("@date", OleDbType.Date).Value = ToAccessDateParameter(DateTime.Now);
                insertCmd.Parameters.Add("@status", OleDbType.VarWChar, 50).Value = "Завершена";
                await insertCmd.ExecuteNonQueryAsync();
            }

            string newPropertyStatus = dealType == "Продажа" ? "Продано" : "Арендовано";
            const string updatePropertySql = "UPDATE Properties SET [Status] = ? WHERE PropertyID = ? AND [Status] = ?";
            using (var updateCmd = new OleDbCommand(updatePropertySql, connection, transaction))
            {
                updateCmd.Parameters.Add("@status", OleDbType.VarWChar, 50).Value = newPropertyStatus;
                updateCmd.Parameters.Add("@propertyId", OleDbType.Integer).Value = propertyId;
                updateCmd.Parameters.Add("@activeStatus", OleDbType.VarWChar, 50).Value = "Активно";
                int affected = await updateCmd.ExecuteNonQueryAsync();
                if (affected == 0)
                {
                    throw new InvalidOperationException("Объект уже не активен. Оформление сделки отменено.");
                }
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<List<string>> GetDealsReportByPeriodAsync(DateTime startDate, DateTime endDate)
    {
        using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync();
        if (ResolveSchemaMode(connection) == SchemaMode.Russian)
        {
            const string sql = @"
                SELECT s.ID_Сделки, s.Дата_Сделки, 'Продажа', s.Цена_Продажи, 'Завершена',
                       (к.Фамилия + ' ' + к.Имя + ' ' + к.Отчество) AS Клиент, o.Адрес
                FROM (Сделки s
                INNER JOIN Клиенты к ON s.Покупатель = к.ID_Клиента)
                INNER JOIN Объекты_Недвижимости o ON s.Объект = o.ID_Объекта
                WHERE s.Дата_Сделки >= ? AND s.Дата_Сделки <= ?
                ORDER BY s.Дата_Сделки";

            return await ReadDealLinesAsync(connection, sql, cmd =>
            {
                cmd.Parameters.AddWithValue("@start", startDate.Date);
                cmd.Parameters.AddWithValue("@end", endDate.Date.AddDays(1).AddSeconds(-1));
            });
        }

        const string standardSql = @"
            SELECT d.DealID, d.DealDate, d.DealType, d.Amount, d.[Status], c.FullName, p.[Address]
            FROM (Deals d 
            INNER JOIN Clients c ON d.ClientID = c.ClientID)
            INNER JOIN Properties p ON d.PropertyID = p.PropertyID
            WHERE d.DealDate >= ? AND d.DealDate <= ?
            ORDER BY d.DealDate";

        return await ReadDealLinesAsync(connection, standardSql, cmd =>
        {
            cmd.Parameters.AddWithValue("@start", startDate.Date);
            cmd.Parameters.AddWithValue("@end", endDate.Date.AddDays(1).AddSeconds(-1));
        });
    }

    public async Task<List<string>> GetClientDealsReportAsync(int clientId)
    {
        using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync();
        if (ResolveSchemaMode(connection) == SchemaMode.Russian)
        {
            const string sql = @"
                SELECT s.ID_Сделки, s.Дата_Сделки, 'Продажа', s.Цена_Продажи, 'Завершена', o.Адрес, o.Тип_Объекта
                FROM Сделки s
                INNER JOIN Объекты_Недвижимости o ON s.Объект = o.ID_Объекта
                WHERE s.Покупатель = ? OR s.Продавец = ?
                ORDER BY s.Дата_Сделки DESC";
            return await ReadDealLinesAsync(connection, sql, cmd =>
            {
                cmd.Parameters.AddWithValue("@clientId1", clientId);
                cmd.Parameters.AddWithValue("@clientId2", clientId);
            });
        }

        const string standardSql = @"
            SELECT d.DealID, d.DealDate, d.DealType, d.Amount, d.[Status], p.[Address], p.PropertyType
            FROM Deals d
            INNER JOIN Properties p ON d.PropertyID = p.PropertyID
            WHERE d.ClientID = ?
            ORDER BY d.DealDate DESC";

        return await ReadDealLinesAsync(connection, standardSql, cmd => cmd.Parameters.AddWithValue("@clientId", clientId));
    }

    public async Task<List<string>> GetAllUserTableNamesAsync()
    {
        using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync();

        var tableNames = connection
            .GetSchema("Tables")
            .Rows
            .Cast<DataRow>()
            .Where(row => string.Equals(Convert.ToString(row["TABLE_TYPE"]), "TABLE", StringComparison.OrdinalIgnoreCase))
            .Select(row => Convert.ToString(row["TABLE_NAME"]) ?? string.Empty)
            .Where(name => !string.IsNullOrWhiteSpace(name) && !name.StartsWith("MSys", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name)
            .ToList();

        return tableNames;
    }

    public async Task<DataTable> GetTableDataAsync(string tableName)
    {
        var table = new DataTable();
        using var connection = new OleDbConnection(_connectionString);
        await connection.OpenAsync();
        using var command = new OleDbCommand($"SELECT * FROM [{tableName}]", connection);
        using var reader = await command.ExecuteReaderAsync();
        table.Load(reader!);
        return table;
    }

    private async Task<List<Property>> ReadPropertiesAsync(
        OleDbConnection connection,
        string sql,
        Action<OleDbCommand>? fillParams,
        bool russianMode)
    {
        var result = new List<Property>();
        using var command = new OleDbCommand(sql, connection);
        fillParams?.Invoke(command);
        using var reader = await command.ExecuteReaderAsync();
        while (reader is not null && await reader.ReadAsync())
        {
            result.Add(new Property
            {
                PropertyID = reader.GetInt32(0),
                Address = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                PropertyType = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                Area = reader.IsDBNull(3) ? 0 : Convert.ToDouble(reader.GetValue(3)),
                Rooms = reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader.GetValue(4)),
                Floor = reader.IsDBNull(5) ? 0 : Convert.ToInt32(reader.GetValue(5)),
                Price = reader.IsDBNull(6) ? 0 : Convert.ToDecimal(reader.GetValue(6)),
                Status = reader.IsDBNull(7)
                    ? string.Empty
                    : russianMode
                        ? NormalizeUiPropertyStatus(Convert.ToString(reader.GetValue(7)) ?? string.Empty)
                        : reader.GetString(7),
                OwnerID = reader.IsDBNull(8) ? 0 : Convert.ToInt32(reader.GetValue(8))
            });
        }
        return result;
    }

    private async Task<List<Deal>> ReadDealsAsync(
        OleDbConnection connection,
        string sql,
        Action<OleDbCommand>? fillParams)
    {
        var result = new List<Deal>();
        using var command = new OleDbCommand(sql, connection);
        fillParams?.Invoke(command);
        using var reader = await command.ExecuteReaderAsync();
        while (reader is not null && await reader.ReadAsync())
        {
            result.Add(new Deal
            {
                DealID = reader.GetInt32(0),
                PropertyID = Convert.ToInt32(reader.GetValue(1)),
                ClientID = Convert.ToInt32(reader.GetValue(2)),
                DealType = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                Amount = reader.IsDBNull(4) ? 0 : Convert.ToDecimal(reader.GetValue(4)),
                DealDate = reader.IsDBNull(5) ? DateTime.MinValue : Convert.ToDateTime(reader.GetValue(5)),
                Status = reader.IsDBNull(6) ? string.Empty : reader.GetString(6)
            });
        }
        return result;
    }

    private async Task<List<string>> ReadDealLinesAsync(
        OleDbConnection connection,
        string sql,
        Action<OleDbCommand>? fillParams)
    {
        var rows = new List<string>();
        using var command = new OleDbCommand(sql, connection);
        fillParams?.Invoke(command);
        using var reader = await command.ExecuteReaderAsync();
        while (reader is not null && await reader.ReadAsync())
        {
            rows.Add(
                $"Сделка #{reader.GetInt32(0)} | Дата: {Convert.ToDateTime(reader.GetValue(1)):dd.MM.yyyy} | " +
                $"Тип: {reader.GetString(2)} | Сумма: {Convert.ToDecimal(reader.GetValue(3)):N2} | " +
                $"Статус: {reader.GetString(4)} | Клиент/Объект: {reader.GetString(5)} / {reader.GetString(6)}");
        }
        return rows;
    }

    private static void FillPropertyParameters(OleDbCommand command, Property property, bool includeId)
    {
        command.Parameters.AddWithValue("@address", property.Address);
        command.Parameters.AddWithValue("@type", property.PropertyType);
        command.Parameters.AddWithValue("@area", property.Area);
        command.Parameters.AddWithValue("@rooms", property.Rooms);
        command.Parameters.AddWithValue("@floor", property.Floor);
        command.Parameters.AddWithValue("@price", property.Price);
        command.Parameters.AddWithValue("@status", property.Status);
        command.Parameters.AddWithValue("@ownerId", property.OwnerID);
        if (includeId)
        {
            command.Parameters.AddWithValue("@id", property.PropertyID);
        }
    }

    private static DateTime ToAccessDateParameter(DateTime value)
    {
        DateTime d = value.Date;
        return d.Kind == DateTimeKind.Unspecified
            ? d
            : DateTime.SpecifyKind(d, DateTimeKind.Unspecified);
    }

    private static OleDbType GetOleDbTypeForColumn(OleDbConnection connection, string tableName, string columnName, OleDbType fallback)
    {
        try
        {
            DataTable schema = connection.GetSchema("Columns", new[] { null, null, tableName, columnName });
            if (schema.Rows.Count == 0)
            {
                return fallback;
            }

            object? raw = schema.Rows[0]["DATA_TYPE"];
            if (raw is null || raw is DBNull)
            {
                return fallback;
            }

            short typeCode = Convert.ToInt16(raw);
            return (OleDbType)typeCode;
        }
        catch
        {
            return fallback;
        }
    }

    private static object CoerceValue(OleDbType type, object value)
    {
        if (value is DBNull) return DBNull.Value;

        // Text-like
        return type switch
        {
            OleDbType.VarWChar or OleDbType.VarChar or OleDbType.LongVarWChar or OleDbType.LongVarChar =>
                Convert.ToString(value) ?? string.Empty,

            OleDbType.Date or OleDbType.DBDate or OleDbType.DBTime or OleDbType.DBTimeStamp =>
                value is DateTime dt ? dt : Convert.ToDateTime(value),

            OleDbType.Boolean => value is bool b ? b : Convert.ToBoolean(value),

            OleDbType.Integer or OleDbType.SmallInt or OleDbType.TinyInt or OleDbType.UnsignedInt or OleDbType.UnsignedSmallInt =>
                value is int i ? i : Convert.ToInt32(value),

            OleDbType.Currency or OleDbType.Decimal or OleDbType.Numeric or OleDbType.Double or OleDbType.Single =>
                value is decimal m ? m : Convert.ToDecimal(value),

            _ => value
        };
    }

    private static void FillDealParameters(OleDbCommand command, Deal deal, bool includeId)
    {
        // ACE/Jet: без Precision/Scale у Decimal и без «чистой» даты часто вылетает
        // «Несоответствие типов данных в выражении условия отбора» при INSERT в CURRENCY/DATETIME.
        command.Parameters.Add("@propertyId", OleDbType.Integer).Value = deal.PropertyID;
        command.Parameters.Add("@clientId", OleDbType.Integer).Value = deal.ClientID;
        command.Parameters.Add("@type", OleDbType.VarWChar, 50).Value = deal.DealType;
        // В Access поле суммы обычно Currency — передаём как Currency, чтобы избежать type mismatch.
        command.Parameters.Add("@amount", OleDbType.Currency).Value = deal.Amount;

        command.Parameters.Add("@date", OleDbType.Date).Value = ToAccessDateParameter(deal.DealDate);
        command.Parameters.Add("@status", OleDbType.VarWChar, 50).Value = deal.Status;
        if (includeId)
        {
            command.Parameters.Add("@id", OleDbType.Integer).Value = deal.DealID;
        }
    }

    private SchemaMode ResolveSchemaMode(OleDbConnection connection)
    {
        if (_schemaMode != SchemaMode.Unknown)
        {
            return _schemaMode;
        }

        var tableNames = connection
            .GetSchema("Tables")
            .Rows
            .Cast<System.Data.DataRow>()
            .Where(row => string.Equals(Convert.ToString(row["TABLE_TYPE"]), "TABLE", StringComparison.OrdinalIgnoreCase))
            .Select(row => Convert.ToString(row["TABLE_NAME"]) ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _schemaMode = tableNames.Contains("Клиенты")
                      && tableNames.Contains("Объекты_Недвижимости")
                      && tableNames.Contains("Сделки")
            ? SchemaMode.Russian
            : SchemaMode.Standard;

        return _schemaMode;
    }

    private static void ParseFullName(string fullName, out string lastName, out string firstName, out string middleName)
    {
        string[] parts = fullName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        lastName = parts.Length > 0 ? parts[0] : string.Empty;
        firstName = parts.Length > 1 ? parts[1] : string.Empty;
        middleName = parts.Length > 2 ? parts[2] : string.Empty;
    }

    private static string NormalizeUiPropertyStatus(string status) => status switch
    {
        "Свободно" => "Активно",
        "Сдано" => "Арендовано",
        _ => status
    };

    private static string NormalizeRussianPropertyStatus(string status) => status switch
    {
        "Активно" => "Свободно",
    "Активный" => "Свободно",
        "Арендовано" => "Сдано",
    "Сдан" => "Сдано",
        _ => status
    };
}
