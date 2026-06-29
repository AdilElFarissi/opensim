using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using OpenMetaverse;
using OpenSim.Framework;
using MySql.Data.MySqlClient;

namespace OpenSim.Data.MySQL
{
    public class MySqlUserAccountData : MySQLGenericTableHandler<UserAccountData>, IUserAccountData
    {
        public MySqlUserAccountData(string connectionString, string realm)
                : base(connectionString, realm, "UserAccount")
        {
        }

        public UserAccountData[] GetUsers(UUID scopeID, string query)
        {
            string[] words = query.Split();

            bool valid = false;

            for (int i = 0 ; i < words.Length ; i++)
            {
                if (words[i].Length > 2)
                    valid = true;
            }

            if ((!valid) || words.Length == 0)
                return new UserAccountData[0];

            if (words.Length > 2)
                return new UserAccountData[0];

            using (MySqlCommand cmd = new MySqlCommand())
            {
                if (words.Length == 1)
                {
                    cmd.CommandText = String.Format("select * from {0} where (ScopeID=?ScopeID or ScopeID='00000000-0000-0000-0000-000000000000') and (FirstName like ?search or LastName like ?search) and active=1", m_Realm);
                    cmd.Parameters.AddWithValue("?search", "%" + words[0] + "%");
                    cmd.Parameters.AddWithValue("?ScopeID", scopeID.ToString());
                }
                else
                {
                    cmd.CommandText = String.Format("select * from {0} where (ScopeID=?ScopeID or ScopeID='00000000-0000-0000-0000-000000000000') and (FirstName like ?searchFirst and LastName like ?searchLast) and active=1", m_Realm);
                    cmd.Parameters.AddWithValue("?searchFirst", "%" + words[0] + "%");
                    cmd.Parameters.AddWithValue("?searchLast", "%" + words[1] + "%");
                    cmd.Parameters.AddWithValue("?ScopeID", scopeID.ToString());
                }

                return DoQuery(cmd);
            }
        }

        public UserAccountData[] GetUsersWhere(UUID scopeID, string where)
        {
            // Basic input validation to prevent SQL injection.
            if (!string.IsNullOrEmpty(where))
            {
                string[] forbidden = { ";", "--", "/*", "*/" };
                foreach (var token in forbidden)
                {
                    if (where.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                        return new UserAccountData[0];
                }
            }

            using (MySqlCommand cmd = new MySqlCommand())
            {
                if (!scopeID.IsZero())
                {
                    where = "(ScopeID=?ScopeID or ScopeID='00000000-0000-0000-0000-000000000000') and (" + where + ")";
                    cmd.Parameters.AddWithValue("?ScopeID", scopeID.ToString());
                }

                cmd.CommandText = String.Format("select * from {0} where " + where, m_Realm);

                return DoQuery(cmd);
            }
        }
    }
}