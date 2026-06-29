public class PGSQLRegionData : IRegionData
{
    private string m_Realm;
    private List<string> m_ColumnNames = null;
    private string m_ConnectionString;
    private PGSQLManager m_database;
    private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

    protected virtual Assembly Assembly
    {
        get { return GetType().Assembly; }
    }

    public PGSQLRegionData(string connectionString, string realm)
    {
        m_Realm = realm;
        if (!IsValidTableName(m_Realm))
            throw new ArgumentException("Invalid realm name");
        m_ConnectionString = connectionString;
        m_database = new PGSQLManager(connectionString);

        using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
        {
            conn.Open();
            Migration m = new Migration(conn, GetType().Assembly, "GridStore");
            m.Update();
        }
        LoadFieldTypes();
    }

    private bool IsValidTableName(string name)
    {
        return Regex.IsMatch(name, @"^[a-zA-Z0-9_]+$");
    }

    private void LoadFieldTypes()
    {
        m_FieldTypes = new Dictionary<string, string>();

        string query = string.Format(@"select column_name,data_type
                    from INFORMATION_SCHEMA.COLUMNS
                   where table_name = lower(@0);", m_Realm);
        using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
        using (NpgsqlCommand cmd = new NpgsqlCommand(query, conn))
        {
            cmd.Parameters.AddWithValue("@0", m_Realm);
            conn.Open();
            using (NpgsqlDataReader rdr = cmd.ExecuteReader())
            {
                while (rdr.Read())
                {
                    m_FieldTypes.Add((string)rdr[0], (string)rdr[1]);
                }
            }
        }
    }

    public List<RegionData> Get(string regionName, UUID scopeID)
    {
        string sql = "select * from " + m_Realm + " where lower(\"regionName\") like lower(@regionName)";
        if (!scopeID.IsZero())
            sql += " and \"ScopeID\" = @scopeID";
        sql += " order by lower(\"regionName\")";

        using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
        using (NpgsqlCommand cmd = new NpgsqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("@regionName", regionName);
            if (!scopeID.IsZero())
                cmd.Parameters.AddWithValue("@scopeID", scopeID);
            conn.Open();
            return RunCommand(cmd);
        }
    }

    public RegionData GetSpecific(string regionName, UUID scopeID)
    {
        string sql = "select * from " + m_Realm + " where lower(\"regionName\") = lower(@regionName)";
        if (!scopeID.IsZero())
            sql += " and \"ScopeID\" = @scopeID";

        using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
        using (NpgsqlCommand cmd = new NpgsqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("@regionName", regionName);
            if (!scopeID.IsZero())
                cmd.Parameters.AddWithValue("@scopeID", scopeID);
            conn.Open();
            List<RegionData> ret = RunCommand(cmd);
            return ret.Count > 0 ? ret[0] : null;
        }
    }

    public RegionData Get(int posX, int posY, UUID scopeID)
    {
        string sql = "select * from " + m_Realm + " where \"locX\" between @startX and @endX and \"locY\" between @startY and @endY";
        if (!scopeID.IsZero())
            sql += " and \"ScopeID\" = @scopeID";

        int startX = posX - (int)Constants.MaximumRegionSize;
        int startY = posY - (int)Constants.MaximumRegionSize;
        int endX = posX;
        int endY = posY;

        using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
        using (NpgsqlCommand cmd = new NpgsqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("@startX", startX);
            cmd.Parameters.AddWithValue("@startY", startY);
            cmd.Parameters.AddWithValue("@endX", endX);
            cmd.Parameters.AddWithValue("@endY", endY);
            if (!scopeID.IsZero())
                cmd.Parameters.AddWithValue("@scopeID", scopeID);
            conn.Open();
            List<RegionData> ret = RunCommand(cmd);
            foreach (RegionData r in ret)
            {
                if (posX >= r.posX && posX < r.posX + r.sizeX && posY >= r.posY && posY < r.posY + r.sizeY)
                    return r;
            }
            return null;
        }
    }

    public RegionData Get(UUID regionID, UUID scopeID)
    {
        string sql = "select * from " + m_Realm + " where uuid = @regionID";
        if (!scopeID.IsZero())
            sql += " and \"ScopeID\" = @scopeID";

        using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
        using (NpgsqlCommand cmd = new NpgsqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("@regionID", regionID);
            if (!scopeID.IsZero())
                cmd.Parameters.AddWithValue("@scopeID", scopeID);
            conn.Open();
            List<RegionData> ret = RunCommand(cmd);
            return ret.Count > 0 ? ret[0] : null;
        }
    }

    public List<RegionData> Get(int startX, int startY, int endX, int endY, UUID scopeID)
    {
        string sql = "select * from " + m_Realm + " where \"locX\" between @qstartX and @endX and \"locY\" between @qstartY and @endY";
        if (!scopeID.IsZero())
            sql += " and \"ScopeID\" = @scopeID";

        int qstartX = startX - (int)Constants.MaximumRegionSize;
        int qstartY = startY - (int)Constants.MaximumRegionSize;

        using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
        using (NpgsqlCommand cmd = new NpgsqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("@qstartX", qstartX);
            cmd.Parameters.AddWithValue("@qstartY", qstartY);
            cmd.Parameters.AddWithValue("@endX", endX);
            cmd.Parameters.AddWithValue("@endY", endY);
            if (!scopeID.IsZero())
                cmd.Parameters.AddWithValue("@scopeID", scopeID);
            conn.Open();
            List<RegionData> dbret = RunCommand(cmd);
            List<RegionData> ret = new List<RegionData>();
            foreach (RegionData r in dbret)
            {
                if (r.posX + r.sizeX > startX && r.posX <= endX && r.posY + r.sizeY > startY && r.posY <= endY)
                    ret.Add(r);
            }
            return ret;
        }
    }

    public List<RegionData> RunCommand(NpgsqlCommand cmd)
    {
        List<RegionData> retList = new List<RegionData>();

        NpgsqlDataReader result = cmd.ExecuteReader();

        while (result.Read())
        {
            RegionData ret = new RegionData();
            ret.Data = new Dictionary<string, object>();

            UUID regionID;
            UUID.TryParse(result["uuid"].ToString(), out regionID);
            ret.RegionID = regionID;
            UUID scope;
            UUID.TryParse(result["ScopeID"].ToString(), out scope);
            ret.ScopeID = scope;
            ret.RegionName = result["regionName"].ToString();
            ret.posX = Convert.ToInt32(result["locX"]);
            ret.posY = Convert.ToInt32(result["locY"]);
            ret.sizeX = Convert.ToInt32(result["sizeX"]);
            ret.sizeY = Convert.ToInt32(result["sizeY"]);

            if (m_ColumnNames == null)
            {
                DataTable schemaTable = result.GetSchemaTable();
                foreach (DataRow row in schemaTable.Rows)
                    m_ColumnNames.Add(row["ColumnName"].ToString());
            }

            foreach (string s in m_ColumnNames)
            {
                if (s == "uuid" || s == "ScopeID" || s == "regionName" || s == "locX" || s == "locY")
                    continue;
                ret.Data[s] = result[s].ToString();
            }

            retList.Add(ret);
        }
        return retList;
    }

    public bool Store(RegionData data)
    {
        var allowedFields = new[] { "locX", "locY", "sizeX", "sizeY", "regionName" };
        var validFields = data.Data.Keys.Intersect(allowedFields).ToList();

        data.Data.Remove("uuid");
        data.Data.Remove("ScopeID");
        data.Data.Remove("regionName");
        data.Data.Remove("posX");
        data.Data.Remove("posY");
        data.Data.Remove("sizeX");
        data.Data.Remove("sizeY");

        using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
        using (NpgsqlCommand cmd = new NpgsqlCommand())
        {
            string update = "update " + m_Realm + " set \"locX\"=:posX, \"locY\"=:posY, \"sizeX\"=:sizeX, \"sizeY\"=:sizeY ";

            foreach (string field in validFields)
            {
                update += ", \"" + field + "\" = :" + field;
                cmd.Parameters.AddWithValue(field, data.Data[field]);
            }

            update += " where uuid = :regionID";
            if (!data.ScopeID.IsZero())
                update += " and \"ScopeID\" = :scopeID";

            cmd.CommandText = update;
            cmd.Connection = conn;
            cmd.Parameters.AddWithValue("regionID", data.RegionID);
            cmd.Parameters.AddWithValue("regionName", data.RegionName);
            cmd.Parameters.AddWithValue("scopeID", data.ScopeID);
            cmd.Parameters.AddWithValue("posX", data.posX);
            cmd.Parameters.AddWithValue("posY", data.posY);
            cmd.Parameters.AddWithValue("sizeX", data.sizeX);
            cmd.Parameters.AddWithValue("sizeY", data.sizeY);
            conn.Open();
            return cmd.ExecuteNonQuery() > 0;
        }
    }

    public bool SetDataItem(UUID regionID, string item, string value)
    {
        if (!IsValidTableColumn(item))
            return false;
        string sql = "update " + m_Realm +
                " set \"" + item + "\" = @" + item + " where uuid = @UUID";

        using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
        using (NpgsqlCommand cmd = new NpgsqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue(item, value);
            cmd.Parameters.AddWithValue("UUID", regionID);
            conn.Open();
            return cmd.ExecuteNonQuery() > 0;
        }
    }

    private bool IsValidTableColumn(string columnName)
    {
        return m_FieldTypes.ContainsKey(columnName);
    }

    public bool Delete(UUID regionID)
    {
        string sql = "delete from " + m_Realm +
                " where uuid = @UUID";
        using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
        using (NpgsqlCommand cmd = new NpgsqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("UUID", regionID);
            conn.Open();
            return cmd.ExecuteNonQuery() > 0;
        }
    }

    public List<RegionData> GetDefaultRegions(UUID scopeID)
    {
        return Get((int)RegionFlags.DefaultRegion, scopeID);
    }

    public List<RegionData> GetDefaultHypergridRegions(UUID scopeID)
    {
        return Get((int)RegionFlags.DefaultHGRegion, scopeID);
    }

    public List<RegionData> GetFallbackRegions(UUID scopeID)
    {
        return Get((int)RegionFlags.FallbackRegion, scopeID);
    }

    public List<RegionData> GetHyperlinks(UUID scopeID)
    {
        return Get((int)RegionFlags.Hyperlink, scopeID);
    }

    public List<RegionData> GetOnlineRegions(UUID scopeID)
    {
        return Get((int)RegionFlags.RegionOnline, scopeID);
    }

    private List<RegionData> Get(int regionFlags, UUID scopeID)
    {
        string sql = "SELECT * FROM " + m_Realm + " WHERE (\"flags\" & " + regionFlags.ToString() + ") <> 0";
        if (!scopeID.IsZero())
            sql += " AND \"ScopeID\" = @scopeID";

        using (NpgsqlConnection conn = new NpgsqlConnection(m_ConnectionString))
        using (NpgsqlCommand cmd = new NpgsqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("scopeID", scopeID);
            conn.Open();
            return RunCommand(cmd);
        }
    }
}