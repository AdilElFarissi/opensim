using System;
using System.Collections.Generic;
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
            // Split the query into words and remove any that are shorter than 3 characters
            string[] words = query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> filtered = new List<string>();
            foreach (var w in words)
            {
                if (w.Length >= 3)
                    filtered.Add(w);
            }
            words = filtered.ToArray();

            if (words.Length == 0 || words.Length > 2)
                return new UserAccountData[0];

            using (SQLiteCommand cmd = new SQLiteCommand())
            {
                cmd.CommandText = $"SELECT * FROM {m_Realm} WHERE (ScopeID=@scope OR ScopeID='00000000-0000-0000-0000-000000000000')";
                cmd.Parameters.AddWithValue("@scope", scopeID.ToString());

                if (words.Length == 1)
                {
                    cmd.CommandText += " AND (FirstName LIKE @p0 OR LastName LIKE @p0)";
                    cmd.Parameters.AddWithValue("@p0", words[0] + "%");
                }
                else // words.Length == 2
                {
                    cmd.CommandText += " AND (FirstName LIKE @p0 OR LastName LIKE @p1)";
                    cmd.Parameters.AddWithValue("@p0", words[0] + "%");
                    cmd.Parameters.AddWithValue("@p1", words[1] + "%");
                }

                return DoQuery(cmd);
            }
        }

        public UserAccountData[] GetUsersWhere(UUID scopeID, string where)
        {
            // This method is intentionally not implemented to avoid arbitrary SQL injection.
            // callers should use GetUsers or a safe parameterized query instead.
            throw new NotImplementedException("GetUsersWhere is not supported due to security concerns.");
        }
    }
}