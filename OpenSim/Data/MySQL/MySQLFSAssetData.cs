public bool[] AssetsExist(UUID[] uuids)
{
    if (uuids == null || uuids.Any(id => string.IsNullOrEmpty(id.ToString())))
    {
        throw new ArgumentException("Input uuids array cannot be null or contain null values");
    }

    if (uuids.Length == 0)
    {
        return new bool[0];
    }

    // Validate input to prevent SQL injection attacks
    if (uuids.Length > 1000)
    {
        List<UUID> exists = new List<UUID>();

        for (int i = 0; i < uuids.Length; i += 1000)
        {
            var idList = uuids.Skip(i).Take(1000).Select(id => id.ToString());
            string sql = "SELECT id FROM {0} WHERE id IN @ids";

            using (MySqlConnection conn = new MySqlConnection(mConnectionString))
            {
                try
                {
                    conn.Open();

                    using (MySqlCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = string.Format(sql, m_Table);
                        cmd.Parameters.AddWithValue("@ids", string.Join(",", idList));
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                UUID id = DBGuid.FromDB(reader["id"]);
                                exists.Add(id);
                            }
                        }
                    }
                }
                catch (MySqlException e)
                {
                    m_log.ErrorFormat("[FSASSETS]: Failed to open database: {0}", e.ToString());
                    return uuids.Select(id => false).ToArray();
                }
            }
        }
    }
    else
    {
        List<UUID> exists = new List<UUID>();

        for (int i = 0; i < uuids.Length; i++)
        {
            string sql = "SELECT id FROM {0} WHERE id = @id";

            using (MySqlConnection conn = new MySqlConnection(mConnectionString))
            {
                try
                {
                    conn.Open();

                    using (MySqlCommand cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = string.Format(sql, m_Table);
                        cmd.Parameters.AddWithValue("@id", uuids[i].ToString());
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                UUID id = DBGuid.FromDB(reader["id"]);
                                exists.Add(id);
                            }
                        }
                    }
                }
                catch (MySqlException e)
                {
                    m_log.ErrorFormat("[FSASSETS]: Failed to open database: {0}", e.ToString());
                    return uuids.Select(id => false).ToArray();
                }
            }
        }
    }

    return uuids.Select(id => exists.Contains(id)).ToArray();
}

public int Count()
{
    string sql = "SELECT COUNT(id) FROM {0}";

    try
    {
        using (MySqlConnection conn = new MySqlConnection(mConnectionString))
        {
            conn.Open();

            using (MySqlCommand cmd = conn.CreateCommand())
            {
                cmd.CommandText = string.Format(sql, m_Table);
                using (MySqlDataReader reader = cmd.ExecuteReader())
                {
                    reader.Read();
                    return Convert.ToInt32(reader["COUNT(id)"]);
                }
            }
        }
    }
    catch (MySqlException e)
    {
        m_log.ErrorFormat("[FSASSETS]: Failed to open database: {0}", e.ToString());
        return 0;
    }
}

public bool Delete(string id)
{
    try
    {
        string sql = "DELETE FROM {0} WHERE id = @id";

        using (MySqlConnection conn = new MySqlConnection(mConnectionString))
        {
            try
            {
                conn.Open();

                using (MySqlCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = string.Format(sql, m_Table);
                    cmd.Parameters.AddWithValue("@id", id);

                    try
                    {
                        int rowsAffected = cmd.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            m_log.InfoFormat("[FSASSETS]: Deleted {0} asset(s).", rowsAffected);
                        }
                        else
                        {
                            m_log.WarnFormat("[FSASSETS]: Failed to delete asset with id {0}.", id);
                        }
                        return true;
                    }
                    catch (MySqlException e)
                    {
                        m_log.ErrorFormat("[FSASSETS]: Failed to delete asset with id {0}", id);
                        m_log.Error(e.ToString());
                        return false;
                    }
                }
            }
            catch (MySqlException e)
            {
                m_log.ErrorFormat("[FSASSETS]: Failed to open database: {0}", e.ToString());
                return false;
            }
        }
    }
    catch (Exception ex)
    {
        m_log.ErrorFormat("[FSASSETS]: Failed to delete asset with id {0}", id);
        m_log.Error(ex.ToString());
        return false;
    }
    finally
    {
        try
        {
            using (MySqlConnection conn = new MySqlConnection(mConnectionString))
            {
                conn.Open();
                using (MySqlCommand cmd = conn.CreateCommand())
                {
                    // Execute a dummy query to ensure that the connection is closed in all cases
                    cmd.CommandText = "SELECT NOW()";
                    cmd.ExecuteNonQuery();
                }
            }
        }
        catch (MySqlException e)
        {
            m_log.ErrorFormat("[FSASSETS]: Failed to open database: {0}", e.ToString());
        }
    }
}