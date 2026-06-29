using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Data.SQLite
{
    public class SQLiteUserAccountData : SQLiteGenericTableHandler<UserAccountData>, IUserAccountData
    {
        public SQLiteUserAccountData(string connectionString, string realm)
                : base(connectionString, realm, "UserAccount")
        {
        }

        public UserAccountData[] GetUsers(UUID scopeID, string query)
        {
            // Split on whitespace and remove short words (<3 chars) safely
            string[] words = query.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            List<string> filtered = new List<string>();
            foreach (string w in words)
            {
                if (w.Length >= 3)
                    filtered.Add(w);
            }
            words = filtered.ToArray();

            if (words.Length == 0 || words.Length > 2)
                return new UserAccountData[0];

            using (SQLiteCommand cmd = new SQLiteCommand())
            {
                cmd.Parameters.AddWithValue("@ScopeID1", scopeID.ToString());
                cmd.Parameters.AddWithValue("@ScopeID2", UUID.Zero.ToString());

                if (words.Length == 1)
                {
                    cmd.CommandText = $"SELECT * FROM {m_Realm} WHERE (ScopeID = @ScopeID1 OR ScopeID = @ScopeID2) " +
                                      "AND (FirstName LIKE @NamePattern OR LastName LIKE @NamePattern)";
                    cmd.Parameters.AddWithValue("@NamePattern", words[0] + "%");
                }
                else // two words
                {
                    cmd.CommandText = $"SELECT * FROM {m_Realm} WHERE (ScopeID = @ScopeID1 OR ScopeID = @ScopeID2) " +
                                      "AND (FirstName LIKE @FirstPattern OR LastName LIKE @LastPattern)";
                    cmd.Parameters.AddWithValue("@FirstPattern", words[0] + "%");
                    cmd.Parameters.AddWithValue("@LastPattern", words[1] + "%");
                }

                return DoQuery(cmd);
            }
        }

        public UserAccountData[] GetUsersWhere(UUID scopeID, string where)
        {
            // Guard against null or empty where clause to avoid malformed SQL.
            if (string.IsNullOrWhiteSpace(where))
                return new UserAccountData[0];

            using (SQLiteCommand cmd = new SQLiteCommand())
            {
                cmd.CommandText = $"SELECT * FROM {m_Realm} WHERE (ScopeID = @ScopeID1 OR ScopeID = @ScopeID2) AND ({where})";
                cmd.Parameters.AddWithValue("@ScopeID1", scopeID.ToString());
                cmd.Parameters.AddWithValue("@ScopeID2", UUID.Zero.ToString());

                // NOTE: The caller is responsible for providing a safe 'where' clause.
                // This method does not attempt to parse or sanitize the clause beyond the
                // scopeID restrictions above to preserve existing behavior while
                // preventing SQL injection through scope parameters.
                return DoQuery(cmd);
            }
        }
    }
}