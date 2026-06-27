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
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using log4net;

namespace OpenSim.Data
{
    /// <summary>
    ///
    /// The Migration theory is based on the ruby on rails concept.
    /// Each database driver is going to be allowed to have files in
    /// Resources that specify the database migrations.  They will be
    /// of the form:
    ///
    ///    001_Users.sql
    ///    002_Users.sql
    ///    003_Users.sql
    ///    001_Prims.sql
    ///    002_Prims.sql
    ///    ...etc...
    ///
    /// When a database driver starts up, it specifies a resource that
    /// needs to be brought up to the current revision.  For instance:
    ///
    ///    Migration um = new Migration(DbConnection, Assembly, "Users");
    ///    um.Update();
    ///
    /// This works out which version Users is at, and applies all the
    /// revisions past it to it.  If there is no users table, all
    /// revisions are applied in order.  Consider each future
    /// migration to be an incremental roll forward of the tables in
    /// question.
    ///
    /// Assembly must be specifically passed in because otherwise you
    /// get the assembly that Migration.cs is part of, and what you
    /// really want is the assembly of your database class.
    ///
    /// </summary>
    public class Migration
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected string _type;
        protected DbConnection _conn;
        protected Assembly _assem;

        private Regex _match_old;
        private Regex _match_new;

        /// <summary>Have the parameterless constructor just so we can specify it as a generic parameter with the new() constraint.
        /// Currently this is only used in the tests. A Migration instance created this way must be then
        /// initialized with Initialize(). Regular creation should be through the parameterized constructors.
        /// </summary>
        public Migration()
        {
        }

        public Migration(DbConnection conn, Assembly assem, string subtype, string type)
        {
            Initialize(conn, assem, type, subtype);
        }

        public Migration(DbConnection conn, Assembly assem, string type)
        {
            Initialize(conn, assem, type, "");
        }

        /// <summary>Must be called after creating with the parameterless constructor.
        /// NOTE that the Migration class now doesn't access database in any way during initialization.
        /// Specifically, it won't check if the [migrations] table exists. Such checks are done later:
        /// automatically on Update(), or you can explicitly call InitMigrationsTable().
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="assem"></param>
        /// <param name="subtype"></param>
        /// <param name="type"></param>
public void Initialize(DbConnection conn, Assembly assem, string type, string subtype)
{
    _type = type;
    _conn = conn;
    _assem = assem;
    _match_old = new Regex(subtype + @"\.(\d\d\d)_" + _type + @"\.sql");
    string s = String.IsNullOrEmpty(subtype) ? _type : _type + @"\." + subtype;
    _match_new = new Regex(@"\." + s + @"\.migrations(?:\.(?<ver>\d+)$|.*)");
}

public void InitMigrationsTable()
{
    int ver = FindVersion(_conn, "migrations");
    if (ver <= 0)
    {
        if (ver < 0)
        {
            using (DbCommand cmd = _conn.CreateCommand())
            {
                cmd.CommandText = "create table migrations(name varchar(100), version int)";
                cmd.ExecuteNonQuery();
            }
        }
        InsertVersion("migrations", 1);
    }
}

protected virtual void ExecuteScript(DbConnection conn, string[] script)
{
    using (DbCommand cmd = conn.CreateCommand())
    {
        cmd.CommandTimeout = 0;
        foreach (string sql in script)
        {
            cmd.CommandText = sql;
            try
            {
                cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                throw new Exception(e.Message + " in SQL: " + sql);
            }
        }
    }
}

protected void ExecuteScript(DbConnection conn, string sql)
{
    ExecuteScript(conn, new string[] { sql });
}

protected void ExecuteScript(string sql)
{
    ExecuteScript(_conn, sql);
}

protected void ExecuteScript(string[] script)
{
    ExecuteScript(_conn, script);
}

public void Update()
{
    InitMigrationsTable();

    int version = FindVersion(_conn, _type);

    SortedList<int, string[]> migrations = GetMigrationsAfter(version);
    if (migrations.Count < 1)
        return;

    m_log.InfoFormat("[MIGRATIONS]: Upgrading {0} to latest revision {1}.", _type, migrations.Keys[migrations.Count - 1]);
    m_log.Info("[MIGRATIONS]: NOTE - this may take a while, don't interrupt this process!");
}

protected int FindVersion(DbConnection conn, string name)
{
    using (DbCommand cmd = conn.CreateCommand())
    {
        cmd.CommandText = "SELECT version FROM migrations WHERE name = @name";
        DbParameter param = cmd.CreateParameter();
        param.ParameterName = "@name";
        param.Value = name;
        cmd.Parameters.Add(param);
        object result = cmd.ExecuteScalar();
        if (result == null || result == DBNull.Value)
            return -1;
        return Convert.ToInt32(result);
    }
}

protected void InsertVersion(string name, int version)
{
    using (DbCommand cmd = _conn.CreateCommand())
    {
        cmd.CommandText = "INSERT INTO migrations (name, version) VALUES (@name, @version)";
        DbParameter nameParam = cmd.CreateParameter();
protected virtual int FindVersion(DbConnection conn, string type)
{
    int version = 0;
    using (DbCommand cmd = conn.CreateCommand())
    {
        try
        {
            cmd.CommandText = "select version from migrations where name = @name order by version desc";
            DbParameter nameParam = cmd.CreateParameter();
            nameParam.ParameterName = "@name";
            nameParam.Value = type;
            cmd.Parameters.Add(nameParam);
            using (DbDataReader reader = cmd.ExecuteReader())
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
            // Something went wrong (probably no table), so we're at version -1
            version = -1;
        }
        finally
        {
            cmd.Parameters.Clear();
        }
    }
    return version;
}
                    version = -1;
                }
            }
            return version;
        }

        private void InsertVersion(string type, int version)
        {
            m_log.InfoFormat("[MIGRATIONS]: Creating {0} at version {1}", type, version);
            ExecuteScript("insert into migrations(name, version) values('" + type + "', " + version + ")");
        }

        private void UpdateVersion(string type, int version)
        {
            m_log.InfoFormat("[MIGRATIONS]: Updating {0} to version {1}", type, version);
            ExecuteScript("update migrations set version=" + version + " where name='" + type + "'");
        }

        private delegate void FlushProc();

        /// <summary>Scans for migration resources in either old-style "scattered" (one file per version)
        /// or new-style "integrated" format (single file with ":VERSION nnn" sections).
        /// In the new-style migrations it also recognizes ':GO' separators for parts of the SQL script
        /// that must be sent to the server separately.  The old-style migrations are loaded each in one piece
        /// and don't support the ':GO' feature.
        /// </summary>
        /// <param name="after">The version we are currently at. Scan for any higher versions</param>
        /// <returns>A list of string arrays, representing the scripts.</returns>
        private SortedList<int, string[]> GetMigrationsAfter(int after)
        {
            SortedList<int, string[]> migrations = new SortedList<int, string[]>();

            string[] names = _assem.GetManifestResourceNames();
            if (names.Length == 0)     // should never happen
                return migrations;

            Array.Sort(names);  // we want all the migrations ordered

            int nLastVerFound = 0;
            Match m = null;
            string sFile = Array.FindLast(names, nm => { m = _match_new.Match(nm); return m.Success; });  // ; nm.StartsWith(sPrefix, StringComparison.InvariantCultureIgnoreCase

            if ((m != null) && !String.IsNullOrEmpty(sFile))
            {
                /* The filename should be '<StoreName>.migrations[.NNN]' where NNN
                 * is the last version number defined in the file. If the '.NNN' part is recognized, the code can skip
                 * the file without looking inside if we have a higher version already. Without the suffix we read
                 * the file anyway and use the version numbers inside.  Any unrecognized suffix (such as '.sql')
                 * is valid but ignored.
                 *
                 *  NOTE that we expect only one 'merged' migration file. If there are several, we take the last one.
                 *  If you are numbering them, leave only the latest one in the project or at least make sure they numbered
                 *  to come up in the correct order (e.g. 'SomeStore.migrations.001' rather than 'SomeStore.migrations.1')
                 */

                if (m.Groups.Count > 1 && int.TryParse(m.Groups[1].Value, out nLastVerFound))
                {
                    if (nLastVerFound <= after)
                        goto scan_old_style;
                }

                System.Text.StringBuilder sb = new System.Text.StringBuilder(4096);
                int nVersion = -1;

                List<string> script = new List<string>();

                FlushProc flush = delegate()
                {
                    if (sb.Length > 0)     // last SQL stmt to script list
                    {
                        script.Add(sb.ToString());
                        sb.Length = 0;
                    }

                    if ((nVersion > 0) && (nVersion > after) && (script.Count > 0) && !migrations.ContainsKey(nVersion))   // script to the versioned script list
                    {
                        migrations[nVersion] = script.ToArray();
                    }
                    script.Clear();
                };

                using (Stream resource = _assem.GetManifestResourceStream(sFile))
                using (StreamReader resourceReader = new StreamReader(resource))
                {
                    int nLineNo = 0;
                    while (!resourceReader.EndOfStream)
                    {
                        string sLine = resourceReader.ReadLine();
                        nLineNo++;

                        if (String.IsNullOrEmpty(sLine) || sLine.StartsWith("#"))  // ignore a comment or empty line
                            continue;

                        if (sLine.Trim().Equals(":GO", StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (sb.Length == 0) continue;
                            if (nVersion > after)
                                script.Add(sb.ToString());
                            sb.Length = 0;
                            continue;
                        }

                        if (sLine.StartsWith(":VERSION ", StringComparison.InvariantCultureIgnoreCase))  // ":VERSION nnn"
                        {
                            flush();

                            int n = sLine.IndexOf('#');     // Comment is allowed in version sections, ignored
                            if (n >= 0)
                                sLine = sLine.Substring(0, n);

                            if (!int.TryParse(sLine.Substring(9).Trim(), out nVersion))
                            {
                                m_log.ErrorFormat("[MIGRATIONS]: invalid version marker at {0}: line {1}. Migration failed!", sFile, nLineNo);
                                break;
                            }
                        }
                        else
                        {
                            sb.AppendLine(sLine);
                        }
                    }
                    flush();

                    // If there are scattered migration files as well, only look for those with higher version numbers.
                    if (after < nVersion)
                        after = nVersion;
                }
            }

scan_old_style:
            // scan "old style" migration pieces anyway, ignore any versions already filled from the single file
            foreach (string s in names)
            {
                m = _match_old.Match(s);
                if (m.Success)
                {
                    int version = int.Parse(m.Groups[1].ToString());
                    if ((version > after) && !migrations.ContainsKey(version))
                    {
                        using (Stream resource = _assem.GetManifestResourceStream(s))
                        {
                            using (StreamReader resourceReader = new StreamReader(resource))
                            {
                                string sql = resourceReader.ReadToEnd();
                                migrations.Add(version, new string[]{sql});
                            }
                        }
                    }
                }
            }

            if (migrations.Count < 1)
                m_log.DebugFormat("[MIGRATIONS]: {0} data tables already up to date at revision {1}", _type, after);

            return migrations;
        }
    }
}
