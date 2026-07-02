using Npgsql;
using System;
using System.Data;
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
            NpgsqlConnection lcConn = (NpgsqlConnection)conn;

            using (NpgsqlCommand cmd = lcConn.CreateCommand())
            {
                try
                {
                    cmd.CommandText = "select version from migrations where name = @type " +
                                      " order by version desc limit 1";
                    cmd.Parameters.Add("@type", NpgsqlTypes.NpgsqlDbType.Text).Value = type;
                    using (NpgsqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            version = Convert.ToInt32(reader["version"]);
                        }
                        reader.Close();
                    }
                }
                catch
                {
                    // Return -1 to indicate table does not exist
                    return -1;
                }
            }
            return version;
        }

        protected override void ExecuteScript(DbConnection conn, string[] script)
        {
            if (!(conn is NpgsqlConnection))
            {
                base.ExecuteScript(conn, script);
                return;
            }

            foreach (string sql in script)
            {
                try
                {
                    using (NpgsqlCommand cmd = new NpgsqlCommand(sql, (NpgsqlConnection)conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
                catch (Exception)
                {
                    throw new Exception(sql);
                }
            }
        }
    }
}