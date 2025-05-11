using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Tutorial9.Model;

namespace Tutorial9.Services;

public class DbService : IDbService
{
    private readonly IConfiguration _configuration;

    public DbService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task DoSomethingAsync()
    {
        await using SqlConnection connection = new SqlConnection(_configuration.GetConnectionString("Default"));
        await using SqlCommand command = new SqlCommand();

        command.Connection = connection;
        await connection.OpenAsync();

        DbTransaction transaction = await connection.BeginTransactionAsync();
        command.Transaction = transaction as SqlTransaction;

        try
        {
            command.CommandText = "INSERT INTO Animal VALUES (@IdAnimal, @Name);";
            command.Parameters.AddWithValue("@IdAnimal", 1);
            command.Parameters.AddWithValue("@Name", "Animal1");

            await command.ExecuteNonQueryAsync();

            command.Parameters.Clear();
            command.CommandText = "INSERT INTO Animal VALUES (@IdAnimal, @Name);";
            command.Parameters.AddWithValue("@IdAnimal", 2);
            command.Parameters.AddWithValue("@Name", "Animal2");

            await command.ExecuteNonQueryAsync();

            await transaction.CommitAsync();
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task ProcedureAsync()
    {
        await using SqlConnection connection = new SqlConnection(_configuration.GetConnectionString("Default"));
        await using SqlCommand command = new SqlCommand();

        command.Connection = connection;
        await connection.OpenAsync();

        command.CommandText = "NazwaProcedury";
        command.CommandType = CommandType.StoredProcedure;

        command.Parameters.AddWithValue("@Id", 2);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<int> AddProductToWarehouseAsync(ProductWarehouseRequest request)
    {
        await using var connection = new SqlConnection(_configuration.GetConnectionString("Default"));
        await using var command = new SqlCommand();
        command.Connection = connection;
        await connection.OpenAsync();

        var transaction = await connection.BeginTransactionAsync();
        command.Transaction = transaction as SqlTransaction;

        try
        {
            command.CommandText = "SELECT 1 FROM Product WHERE IdProduct = @IdProduct";
            command.Parameters.AddWithValue("@IdProduct", request.IdProduct);
            var exists = await command.ExecuteScalarAsync();
            if (exists is null)
                throw new Exception("Produkt nie istnieje");

            command.Parameters.Clear();

            command.CommandText = "SELECT 1 FROM Warehouse WHERE IdWarehouse = @IdWarehouse";
            command.Parameters.AddWithValue("@IdWarehouse", request.IdWarehouse);
            exists = await command.ExecuteScalarAsync();
            if (exists is null)
                throw new Exception("Magazyn nie istnieje");

            command.Parameters.Clear();

            command.CommandText = @"
                SELECT TOP 1 IdOrder FROM [Order]
                WHERE IdProduct = @IdProduct AND Amount = @Amount AND CreatedAt < @CreatedAt";
            command.Parameters.AddWithValue("@IdProduct", request.IdProduct);
            command.Parameters.AddWithValue("@Amount", request.Amount);
            command.Parameters.AddWithValue("@CreatedAt", request.CreatedAt);

            var idOrder = await command.ExecuteScalarAsync();
            if (idOrder is null)
                throw new Exception("Brak pasującego zamówienia");

            command.Parameters.Clear();

            command.CommandText = "SELECT 1 FROM Product_Warehouse WHERE IdOrder = @IdOrder";
            command.Parameters.AddWithValue("@IdOrder", (int)idOrder);
            exists = await command.ExecuteScalarAsync();
            if (exists is not null)
                throw new Exception("Zamówienie już zrealizowane");

            command.Parameters.Clear();

            command.CommandText = "UPDATE [Order] SET FulfilledAt = @Now WHERE IdOrder = @IdOrder";
            command.Parameters.AddWithValue("@Now", DateTime.Now);
            command.Parameters.AddWithValue("@IdOrder", (int)idOrder);
            await command.ExecuteNonQueryAsync();

            command.Parameters.Clear();

            command.CommandText = "SELECT Price FROM Product WHERE IdProduct = @IdProduct";
            command.Parameters.AddWithValue("@IdProduct", request.IdProduct);
            var price = (decimal)(await command.ExecuteScalarAsync() ?? throw new Exception("Brak ceny produktu"));

            command.Parameters.Clear();

            command.CommandText = @"
                INSERT INTO Product_Warehouse (IdWarehouse, IdProduct, IdOrder, Amount, Price, CreatedAt)
                OUTPUT INSERTED.IdProductWarehouse
                VALUES (@IdWarehouse, @IdProduct, @IdOrder, @Amount, @Price, @CreatedAt)";
            command.Parameters.AddWithValue("@IdWarehouse", request.IdWarehouse);
            command.Parameters.AddWithValue("@IdProduct", request.IdProduct);
            command.Parameters.AddWithValue("@IdOrder", (int)idOrder);
            command.Parameters.AddWithValue("@Amount", request.Amount);
            command.Parameters.AddWithValue("@Price", price * request.Amount);
            command.Parameters.AddWithValue("@CreatedAt", DateTime.Now);

            var result = await command.ExecuteScalarAsync();

            await transaction.CommitAsync();
            return (int)result!;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<int> AddProductToWarehouseUsingProcedureAsync(ProductWarehouseRequest request)
    {
        await using var connection = new SqlConnection(_configuration.GetConnectionString("Default"));
        await using var command = new SqlCommand("AddProductToWarehouse", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.AddWithValue("@IdProduct", request.IdProduct);
        command.Parameters.AddWithValue("@IdWarehouse", request.IdWarehouse);
        command.Parameters.AddWithValue("@Amount", request.Amount);
        command.Parameters.AddWithValue("@CreatedAt", DateTime.Now);

        await connection.OpenAsync();

        try
        {
            var result = await command.ExecuteScalarAsync();
            if (result == null)
                throw new Exception("Błąd podczas wykonywania procedury składowanej. Brak zwróconego Id.");

            return Convert.ToInt32(result);
        }
        catch (SqlException ex)
        {
            throw new Exception("Błąd podczas wykonywania procedury składowanej: " + ex.Message);
        }
    }
}
