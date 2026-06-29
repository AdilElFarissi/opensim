using System;
using System.IO;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Text.RegularExpressions;
using log4net;
using log4net.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Tests.Common;

namespace OpenSim.Data.Tests
{
    /// <summary>This is a base class for testing any Data service for any DBMS.
    /// Requires NUnit 2.5 or better (to support the generics).
    /// </summary>
    /// <remarks>
    /// FIXME: Should extend OpenSimTestCase but compile on mono 2.4.3 currently fails with
    /// AssetTests`2 : System.MemberAccessException : Cannot create an instance of OpenSim.Data.Tests.AssetTests`2[TConn,TAssetData] because Type.ContainsGenericParameters is true.
    /// and similar on EstateTests, InventoryTests and RegionTests.
    /// Runs fine with mono 2.10.8.1, so easiest thing is to wait until min Mono version uplifts.
    /// </remarks>
    /// <typeparam name="TConn"></typeparam>
    /// <typeparam name="TService"></typeparam>
    public class BasicDataServiceTest<TConn, TService>
        where TConn : DbConnection, new()
        where TService : class, new()
    {
        protected string m_connStr;
        private TService m_service;
        private string m_file;

        protected ILog m_log;  // doesn't matter here that it's not static, init to correct type in instance .ctor

        public BasicDataServiceTest()
            : this("")
        {
        }

        public BasicDataServiceTest(string conn)
        {
            m_connStr = !string.IsNullOrEmpty(conn) ? conn : DefaultTestConns.Get(typeof(TConn));

            m_log = LogManager.GetLogger(this.GetType());
            OpenSim.Tests.Common.TestLogging.LogToConsole();    // TODO: Is that right?
        }

        /// <summary>
        /// To be overridden in derived classes. Do whatever init with the m_service, like setting the conn string to it.
        /// You'd probably want to to cast the 'service' to a more specific type and store it in a member var.
        /// This framework takes care of disposing it, if it's disposable.
        /// </summary>
        /// <param name="service">The service being tested</param>
        protected virtual void InitService(object service)
        {
        }

        [TestFixtureSetUp]
        public void Init()
        {
            // Sorry, some SQLite-specific stuff goes here (not a big deal, as its just some file ops)
            if (typeof(TConn).Name.StartsWith("Sqlite"))
            {
                // SQLite doesn't work on power or z linux
                if (Directory.Exists("/proc/ppc64") || Directory.Exists("/proc/dasd"))
                    Assert.Ignore();

                if (Util.IsWindows())
                    Util.LoadArchSpecificWindowsDll("sqlite3.dll");

                // for SQLite, if no explicit conn string is specified, use a temp file
                if (string.IsNullOrEmpty(m_connStr))
                {
                    m_file = Path.GetTempFileName() + ".db";
                    m_connStr = "URI=file:" + m_file + ",version=3";
                }
            }

            if (string.IsNullOrEmpty(m_connStr))
            {
                string msg = string.Format("Connection string for {0} is not defined, ignoring tests", typeof(TConn).Name);
                m_log.Warn(msg);
                Assert.Ignore(msg);
            }

            // Try the connection, ignore tests if Open() fails
            using (TConn conn = new TConn())
            {
                conn.ConnectionString = m_connStr;
                try
                {
                    conn.Open();
                    conn.Close();
                }
                catch
                {
                    string msg = string.Format("{0} is unable to connect to the database, ignoring tests", typeof(TConn).Name);
                    m_log.Warn(msg);
                    Assert.Ignore(msg);
                }
            }

            try
            {
                m_service = new TService();
                InitService(m_service);
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                Assert.Ignore();
            }
        }

        [TestFixtureTearDown]
        public void Cleanup()
        {
            if (m_service != null)
            {
                if (m_service is IDisposable)
                    ((IDisposable)m_service).Dispose();
                m_service = null;
            }

            if (!string.IsNullOrEmpty(m_file) && File.Exists(m_file))
                File.Delete(m_file);
        }

        protected virtual DbConnection Connect()
        {
            DbConnection cnn = new TConn
            {
                ConnectionString = m_connStr
            };
            cnn.Open();
            return cnn;
        }

        protected virtual void ExecuteSql(string sql, params DbParameter[] parameters)
        {
            using (DbConnection dbcon = Connect())
            using (DbCommand cmd = dbcon.CreateCommand())
            {
                cmd.CommandText = sql;
                if (parameters != null)
                {
                    foreach (var p in parameters)
                        cmd.Parameters.Add(p);
                }
                cmd.ExecuteNonQuery();
            }
        }

        protected delegate bool ProcessRow(IDataReader reader);

        protected virtual int ExecQuery(string sql, bool bSingleRow, ProcessRow action)
        {
            int nRecs = 0;
            using (DbConnection dbcon = Connect())
            using (DbCommand cmd = dbcon.CreateCommand())
            {
                cmd.CommandText = sql;
                CommandBehavior cb = bSingleRow ? CommandBehavior.SingleRow : CommandBehavior.Default;
                using (DbDataReader rdr = cmd.ExecuteReader(cb))
                {
                    while (rdr.Read())
                    {
                        nRecs++;
                        if (!action(rdr))
                            break;
                    }
                }
            }
            return nRecs;
        }

        private static bool IsSafeIdentifier(string identifier)
        {
            // Allow only letters, numbers, underscore and dot (for schema.table)
            return !string.IsNullOrEmpty(identifier) && Regex.IsMatch(identifier, @"^[A-Za-z0-9_.]+$");
        }

        protected virtual void DropTables(params string[] tables)
        {
            foreach (string tbl in tables)
            {
                if (!IsSafeIdentifier(tbl))
                {
                    m_log.Error($"Unsafe table identifier detected in DropTables: {tbl}");
                    continue;
                }

                try
                {
                    ExecuteSql($"DROP TABLE {tbl};");
                }
                catch
                {
                    // Ignored as per original behavior
                }
            }
        }

        protected virtual void ResetMigrations(params string[] stores)
        {
            if (stores == null || stores.Length == 0)
                return;

            foreach (var store in stores)
            {
                if (!IsSafeIdentifier(store))
                {
                    m_log.Error($"Unsafe store identifier detected in ResetMigrations: {store}");
                    return;
                }
            }

            string lst = string.Join(", ", stores);
            string sCond = stores.Length > 1 ? $"IN ({lst})" : $"= {stores[0]}";

            try
            {
                ExecuteSql($"DELETE FROM migrations where name {sCond}");
            }
            catch
            {
                // Ignored as per original behavior
            }
        }

        protected virtual void ClearTables(params string[] tables)
        {
            foreach (string tbl in tables)
            {
                if (!IsSafeIdentifier(tbl))
                {
                    m_log.Error($"Unsafe table identifier detected in ClearTables: {tbl}");
                    continue;
                }

                try
                {
                    ExecuteSql($"DELETE FROM {tbl};");
                }
                catch
                {
                    // Ignored as per original behavior
                }
            }
        }
    }
}