using Npgsql;
using System;
using System.Data.Common;
using System.Reflection;

namespace OpenSim.Data.PGSQL
{
    public class PGSQLMigration : Migration
    {
        public PGSQLMigration(NpgsqlConnection conn, Assembly assem, string type)
            : base(conn, assem, type)
        {
        }

        public PGSQLMigration(NpgsqlConnection conn, Assembly assem, string subtype, string type)
            : base(conn, assem, subtype, type)
        {
        }

        protected override int FindVersion(DbConnection conn, string type)
        {
            int version = 0;

            if (conn is not NpgsqlConnection lcConn)
                return base.FindVersion(conn, type); // fallback to base implementation if not Npgsql

            using (var cmd = lcConn.CreateCommand())
            {
                // Use parameterized query to prevent SQL injection
                cmd.CommandText = "SELECT version FROM migrations WHERE name = @name ORDER BY version DESC LIMIT 1";
                var param = cmd.CreateParameter();
                param.ParameterName = "@name";
                param.Value = type;
                cmd.Parameters.Add(param);

                try
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            version = Convert.ToInt32(reader["version"]);
                        }
                    }
                }
                catch (Exception)
                {
                    // Return -1 to indicate table does not exist or other error
                    return -1;
                }
            }

            return version;
        }

        protected override void ExecuteScript(DbConnection conn, string[] script)
        {
            if (conn is not NpgsqlConnection)
            {
                base.ExecuteScript(conn, script);
                return;
            }

            foreach (string sql in script)
            {
                try
                {
                    using (var cmd = new NpgsqlCommand(sql, (NpgsqlConnection)conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
                catch (Exception ex)
                {
                    // Preserve original script context while rethrowing
                    throw new Exception($"Error executing SQL script: {sql}", ex);
                }
            }
        }
    }
}