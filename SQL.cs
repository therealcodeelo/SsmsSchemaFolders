using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                if(exist == 0)
                {
                    command.CommandText = "CREATE TABLE dbo.[z.SchFld] (\r\n    ID INT IDENTITY(1,1) PRIMARY KEY,\r\n    schemaName VARCHAR(255),\r\n    pattern VARCHAR(255)\r\n);";
                    command.ExecuteNonQuery();
                }
            }
        }
        public static Dictionary<string,string> GetPatterns()
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                var dict = new Dictionary<string,string>();
                connection.Open();
                var command = new SqlCommand()
                {
                    Connection = connection,
                    CommandText = $"select [schemaName],[pattern] from [z.SchFld]"
                };

                var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    dict.Add(reader.GetString(0), reader.GetString(1));
                }
                return dict;
            }
        }
    }
}
