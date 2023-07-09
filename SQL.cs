using System;
using System.Data.SqlClient;

namespace SsmsSchemaFolders
{
    public static class SQL
    {
        private static string _serverName;
        private static string _databaseName;
        public static string ServerName { get => _serverName; set => _serverName = value; }
        public static string DatabaseName { get => _databaseName; set => _databaseName = value; }
        public static string ConnectionString
        {
            get => $@"Data Source={ServerName};Initial Catalog={DatabaseName};Integrated Security=true;";
        }
        public static void CheckTableExist()
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                var command = new SqlCommand()
                {
                    Connection = connection,
                    CommandText = "IF OBJECT_ID('z.SchFld', 'U') IS NOT NULL\r\n    SELECT '1'\r\nELSE\r\n    SELECT '0'"
                };
                var exist = Convert.ToInt32(command.ExecuteScalar());
                if (exist == 0)
                {
                    command.CommandText = "CREATE TABLE dbo.[z.SchFld] (\r\n    ID INT IDENTITY(1,1) PRIMARY KEY,\r\n    pattern VARCHAR(255)\r\n);";
                    command.ExecuteNonQuery();
                    command.CommandText = "insert into dbo.[z.SchFld](pattern) values('{_ .}[c3][c4][c3][c999]');";
                    command.ExecuteNonQuery();
                }
            }
        }
        public static string GetPatterns()
        {
            try
            {
                using (var connection = new SqlConnection(ConnectionString))
                {
                    connection.Open();
                    var command = new SqlCommand()
                    {
                        Connection = connection,
                        CommandText = $"select top 1 [pattern] from z.SchFld"
                    };

                    var pattern = command.ExecuteScalar() ?? string.Empty;
                    return pattern.ToString();
                }
            }
            catch (Exception)
            {
                return string.Empty;
            }
            
        }
    }
}
