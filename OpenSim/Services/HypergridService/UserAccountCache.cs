using System.Collections.Generic;
using OpenMetaverse;

using OpenSim.Services.Interfaces;

namespace OpenSim.Services.HypergridService
{
    /// <summary>
    /// Provides a caching implementation for user account services.
    /// </summary>
    public class UserAccountCache : IUserAccountService
    {
        private const double CACHE_EXPIRATION_SECONDS = 120000.0; // 33 hours!

//        private static readonly ILog m_log =
//                LogManager.GetLogger(
//                MethodBase.GetCurrentMethod().DeclaringType);

        private readonly ExpiringCache<UUID, UserAccount> m_UUIDCache;

        private readonly IUserAccountService m_UserAccountService;

        private static UserAccountCache m_Singleton;

        /// <summary>
        /// Creates a singleton instance of UserAccountCache.
        /// </summary>
        /// <param name="u">The user account service to wrap.</param>
        /// <returns>The singleton UserAccountCache instance.</returns>
        public static UserAccountCache CreateUserAccountCache(IUserAccountService u)
        {
            if (m_Singleton == null)
                m_Singleton = new UserAccountCache(u);

            return m_Singleton;
        }

        /// <summary>
        /// Initializes a new instance of the UserAccountCache class.
        /// </summary>
        /// <param name="u">The user account service to wrap.</param>
        private UserAccountCache(IUserAccountService u)
        {
            m_UUIDCache = new ExpiringCache<UUID, UserAccount>();
            m_UserAccountService = u;
        }

        /// <summary>
        /// Caches a user account for the specified user ID.
        /// </summary>
        /// <param name="userID">The user ID to cache.</param>
        /// <param name="account">The user account to cache.</param>
        public void Cache(UUID userID, UserAccount account)
        {
            // Cache even null accounts
            m_UUIDCache.AddOrUpdate(userID, account, CACHE_EXPIRATION_SECONDS);

            //m_log.DebugFormat("[USER CACHE]: cached user {0}", userID);
        }

        /// <summary>
        /// Retrieves a user account from the cache.
        /// </summary>
        /// <param name="userID">The user ID to retrieve.</param>
        /// <param name="inCache">Indicates whether the account was found in cache.</param>
        /// <returns>The user account, or null if not found.</returns>
        public UserAccount Get(UUID userID, out bool inCache)
        {
            UserAccount account = null;
            inCache = false;
            if (m_UUIDCache.TryGetValue(userID, out account))
            {
                //m_log.DebugFormat("[USER CACHE]: Account {0} {1} found in cache", account.FirstName, account.LastName);
                inCache = true;
                return account;
            }

            return null;
        }

        /// <summary>
        /// Gets a user account by ID string.
        /// </summary>
        /// <param name="id">The user ID as a string.</param>
        /// <returns>The user account, or null if not found.</returns>
        public UserAccount GetUser(string id)
        {
            UUID uuid = UUID.Zero;
            UUID.TryParse(id, out uuid);
            bool inCache = false;
            UserAccount account = Get(uuid, out inCache);
            if (!inCache)
            {
                account = m_UserAccountService.GetUserAccount(UUID.Zero, uuid);
                Cache(uuid, account);
            }

            return account;
        }

        #region IUserAccountService
        /// <summary>
        /// Gets a user account by scope and user ID.
        /// </summary>
        /// <param name="scopeID">The scope ID.</param>
        /// <param name="userID">The user ID.</param>
        /// <returns>The user account.</returns>
        public UserAccount GetUserAccount(UUID scopeID, UUID userID)
        {
            return GetUser(userID.ToString());
        }

        /// <summary>
        /// Gets a user account by scope, first name, and last name.
        /// </summary>
        /// <param name="scopeID">The scope ID.</param>
        /// <param name="FirstName">The first name.</param>
        /// <param name="LastName">The last name.</param>
        /// <returns>Null implementation.</returns>
        public UserAccount GetUserAccount(UUID scopeID, string FirstName, string LastName)
        {
            return null;
        }

        /// <summary>
        /// Gets a user account by scope and email.
        /// </summary>
        /// <param name="scopeID">The scope ID.</param>
        /// <param name="Email">The email address.</param>
        /// <returns>Null implementation.</returns>
        public UserAccount GetUserAccount(UUID scopeID, string Email)
        {
            return null;
        }

        /// <summary>
        /// Gets user accounts matching a query.
        /// </summary>
        /// <param name="scopeID">The scope ID.</param>
        /// <param name="query">The query string.</param>
        /// <returns>Null implementation.</returns>
        public List<UserAccount> GetUserAccountsWhere(UUID scopeID, string query)
        {
            return null;
        }

        /// <summary>
        /// Gets user accounts matching a query.
        /// </summary>
        /// <param name="scopeID">The scope ID.</param>
        /// <param name="query">The query string.</param>
        /// <returns>Null implementation.</returns>
        public List<UserAccount> GetUserAccounts(UUID scopeID, string query)
        {
            return null;
        }

        /// <summary>
        /// Gets user accounts by IDs.
        /// </summary>
        /// <param name="scopeID">The scope ID.</param>
        /// <param name="IDs">The list of user IDs.</param>
        /// <returns>Null implementation.</returns>
        public List<UserAccount> GetUserAccounts(UUID scopeID, List<string> IDs)
        {
            return null;
        }

        /// <summary>
        /// Invalidates the cache for a specific user.
        /// </summary>
        /// <param name="userID">The user ID to invalidate.</param>
        public void InvalidateCache(UUID userID)
        {
            m_UUIDCache.Remove(userID);
        }

        /// <summary>
        /// Stores a user account.
        /// </summary>
        /// <param name="data">The user account data to store.</param>
        /// <returns>False - not implemented.</returns>
        public bool StoreUserAccount(UserAccount data)
        {
            return false;
        }
        #endregion

    }

}</arg_value>
<arg_key>changes</arg_key>
<arg_value>[{"line": 16, "change": "Added 'readonly' modifier to m_UUIDCache field"}, {"line": 18, "change": "Added 'readonly' modifier to m_UserAccountService field"}, {"line": 1, "change": "Added class summary"}, {"line": 30, "change": "Added method summary for CreateUserAccountCache"}, {"line": 39, "change": "Added method summary for constructor"}, {"line": 47, "change": "Added method summary for Cache"}, {"line": 59, "change": "Added method summary for Get"}, {"line": 77, "change": "Added method summary for GetUser"}, {"line": 103, "change": "Added method summary for GetUserAccount(UUID, UUID)"}, {"line": 111, "change": "Added method summary for GetUserAccount(UUID, string, string)"}, {"line": 120, "change": "Added method summary for GetUserAccount(UUID, string)"}, {"line": 129, "change": "Added method summary for GetUserAccountsWhere"}, {"line": 138, "change": "Added method summary for GetUserAccounts"}, {"line": 147, "change": "Added method summary for GetUserAccounts(UUID, List<string>)"}, {"line": 156, "change": "Added method summary for InvalidateCache"}, {"line": 164, "change": "Added method summary for StoreUserAccount"}]</arg_value>
</tool_call>