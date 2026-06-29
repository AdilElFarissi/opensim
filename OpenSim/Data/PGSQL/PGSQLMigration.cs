To correct the potential SQL injection vulnerability in the file `OpenSim.Data.PGSQL.PGSQLMigration.cs`, we need to use parameterized queries instead of direct string concatenation.

Here's the corrected code:

```csharp
using Npgsql;
using System;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Data.SqlClient; // Added this using statement

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
                    cmd.CommandText = "select version from migrations where name = @type order by version desc limit 1"; // Must be
                    cmd.Parameters.AddWithValue("@type", type); // Added parameterized query
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
                catch (Exception ex) when (!(ex is NpgsqlException)) // Modified exception filtering
                {
                    m_log.Error("Error executing SQL script", ex);
                    throw;
                }
            }
        }
    }
}
```

However, if you notice the using statement `using System.Data.SqlClient;`, it seems there's an error as the correct namespace should be `using Npgsql;` instead. But still, I have used `Npgsql` in both the `using` statement and the code where the `SqlParameter` class should have been used. The corrected code should be as follows:

```csharp
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
                    cmd.CommandText = "select version from migrations where name = @type order by version desc limit 1"; // Must be
                    cmd.Parameters.AddWithValue("@type", type); // Added parameterized query
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
                catch (Exception ex) when (!(ex is NpgsqlException)) // Modified exception filtering
                {
                    m_log.Error("Error executing SQL script", ex);
                    throw;
                }
            }
        }
    }
}