/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
protected override int FindVersion(DbConnection conn, string type)
{
    int version = 0;
    NpgsqlConnection lcConn = (NpgsqlConnection)conn;

    using (NpgsqlCommand cmd = lcConn.CreateCommand())
    {
        try
        {
            cmd.CommandText = "select version from migrations where name = @name order by version desc limit 1";
            cmd.Parameters.AddWithValue("@name", type);
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
                foreach (NpgsqlParameter param in cmd.Parameters)
                {
                    param.Value = null;
                }
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
        }
    }
}
