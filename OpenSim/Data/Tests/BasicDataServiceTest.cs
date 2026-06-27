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
using System.IO;
using System.Collections.Generic;
using log4net.Config;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Tests.Common;
using log4net;
using System.Data;
using System.Data.Common;
using System.Reflection;

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

        // TODO: Is this in the right place here?
        // Later:  apparently it's not, but does it matter here?
//        protected static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected ILog m_log;  // doesn't matter here that it's not static, init to correct type in instance .ctor

        public BasicDataServiceTest()
            : this("")
        {
        }

        public BasicDataServiceTest(string conn)
        {
            m_connStr = !String.IsNullOrEmpty(conn) ? conn : DefaultTestConns.Get(typeof(TConn));

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
                if (String.IsNullOrEmpty(m_connStr))
                {
                    m_file = Path.GetTempFileName() + ".db";
                    m_connStr = "URI=file:" + m_file + ",version=3";
                }
            }

            if (String.IsNullOrEmpty(m_connStr))
            {
                string msg = String.Format("Connection string for {0} is not defined, ignoring tests", typeof(TConn).Name);
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
                    string msg = String.Format("{0} is unable to connect to the database, ignoring tests", typeof(TConn).Name);
                    m_log.Warn(msg);
                    Assert.Ignore(msg);
                }
            }

            // If we manage to connect to the database with the user
            // and password above it is our test database, and run
            // these tests.  If anything goes wrong, ignore these
            // tests.
            try
using (DbConnection dbcon = Connect())
{
    using (DbCommand cmd = dbcon.CreateCommand())
    {
        cmd.CommandText = "DROP TABLE IF EXISTS @table";
        using (DbParameter param = cmd.CreateParameter())
        {
            param.ParameterName = "@table";
            param.Value = tbl;
            cmd.Parameters.Add(param);
        }
        cmd.ExecuteNonQuery();
    }
}
catch (Exception e)
protected virtual void ResetMigrations(params string[] stores)
{
    using (var cmd = new SqlCommand("DELETE FROM migrations WHERE name IN (@names)", m_dbConnection))
    {
        cmd.Parameters.AddWithValue("@names", string.Join(",", stores.Select(s => "'" + s + "'")));
        try
        {
            cmd.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            m_log.Error(e.ToString());
            throw;
        }
    }
}

protected virtual void ClearTables(params string[] tables)
{
    foreach (string tbl in tables)
    {
        using (var cmd = new SqlCommand("DELETE FROM " + tbl, m_dbConnection))
        {
            try
            {
                cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                throw;
            }
        }
    }
}
