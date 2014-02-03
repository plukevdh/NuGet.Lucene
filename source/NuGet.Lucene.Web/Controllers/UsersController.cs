﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Principal;
using System.Web.Http;
using NuGet.Lucene.Web.Authentication;
using NuGet.Lucene.Web.Models;

namespace NuGet.Lucene.Web.Controllers
{
    /// <summary>
    /// <para>
    /// Provides methods to view, create and delete users and api keys for authentication.
    /// </para>
    /// <para>
    /// Clients can authenticate using api keys by setting the <c>X-NuGet-ApiKey</c> header
    /// on requests.
    /// </para>
    /// </summary>
    public class UsersController : ApiController
    {
        public UserStore Store { get; set; }

        /// <summary>
        /// Retrieves a list of all users.
        /// </summary>
        public IEnumerable<ApiUser> GetAllUsers()
        {
            return Store.All.OrderBy(u => u.Username).Select(DescribeUser).ToList();
        }

        /// <summary>
        /// Retrieve information about a user.
        /// </summary>
        public object Get(string username)
        {
            var user = Store.FindByUsername(username);

            if (user == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "User " + username + " not found.");
            }

            return DescribeUser(user);
        }
        
        /// <summary>
        /// Creates or replaces a user.
        /// </summary>
        [Authorize(Roles = RoleNames.AccountAdministrator)]
        public HttpResponseMessage Put(string username, [FromBody]UserAttributes attributes)
        {
            var user = new ApiUser {Username = username, Key = attributes.Key, Roles = attributes.Roles};

            try
            {
                Store.Add(user, GetUserUpdateMode(attributes));
            }
            catch (UserOverwriteException)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Conflict,
                    "User " + username + " already exists.");
            }
            catch (UserPermissionException ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Forbidden, ex.Message);
            }

            return Request.CreateResponse(HttpStatusCode.Created);
        }

        /// <summary>
        /// Updates an existing user, optionally renaming.
        /// </summary>
        /// <param name="username"></param>
        /// <param name="attributes"></param>
        /// <returns></returns>
        [Authorize(Roles = RoleNames.AccountAdministrator)]
        public HttpResponseMessage Post(string username, [FromBody]UpdateUserAttributes attributes)
        {
            try
            {
                Store.Update(username, attributes.RenameTo, attributes.Key, attributes.Roles, GetUserUpdateMode(attributes));
            }
            catch (UserNotFoundException)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "User " + username + " not found.");
            }
            catch (UserOverwriteException)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Conflict, "User " + attributes.RenameTo + " already exists.");
            }
            catch (UserPermissionException ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Forbidden, ex.Message);
            }

            return Request.CreateResponse(HttpStatusCode.OK);
        }

        [Authorize(Roles = RoleNames.AccountAdministrator)]
        public HttpResponseMessage Delete(string username)
        {
            try
            {
                Store.Delete(username);
            }
            catch (UserNotFoundException)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "User " + username + " not found.");
            }
            catch (UserPermissionException ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Forbidden, ex.Message);
            }

            return Request.CreateResponse(HttpStatusCode.OK);
        }

        [Authorize(Roles = RoleNames.AccountAdministrator)]
        public HttpResponseMessage DeleteAllUsers()
        {
            Store.DeleteAll();

            return Request.CreateResponse(HttpStatusCode.OK);
        }

        /// <summary>
        /// Retrieves information about the logged-in user
        /// including name and api key. If the session has
        /// not been authenticated, returns a 206 No Content
        /// response with an empty body.
        /// </summary>
        public object GetAuthenticationInfo()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NoContent, "Not authenticated.");
            }

            return GetRequiredAuthenticationInfo();
        }

        /// <summary>
        /// Retrieves information about the logged-in user
        /// including name and api key. If the request has
        /// not been authenticated, this method will force
        /// authentication to occur before retrieving
        /// information.
        /// </summary>
        [Authorize]
        public ApiUser GetRequiredAuthenticationInfo()
        {
            var apiUser = Store.FindByUsername(User.Identity.Name);

            if (apiUser != null) return apiUser;

            apiUser = new ApiUser { Username = User.Identity.Name, Roles = GetUserRoles(User) };

            Store.Add(apiUser, UserUpdateMode.Overwrite);

            return apiUser;
        }

        /// <summary>
        /// Changes the API key of the authenticated user.
        /// </summary>
        [Authorize]
        [HttpPost]
        public object ChangeApiKey([FromBody]KeyChangeRequest req)
        {
            var username = User.Identity.Name;

            try
            {
                req.Key = Store.ChangeApiKey(username, req.Key);
                return req;
            }
            catch (UserNotFoundException)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "User " + username + " not found.");
            }
            catch (UserPermissionException ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.Forbidden, ex.Message);
            }
        }

        private IEnumerable<string> GetUserRoles(IPrincipal user)
        {
            return RoleNames.All.Where(user.IsInRole);
        }

        private ApiUser DescribeUser(ApiUser user)
        {
            if (User.IsInRole(RoleNames.AccountAdministrator) || IsSelf(user))
            {
                return user;
            }

            // Hide details that non-admins should not see.
            return new ApiUser {Username = user.Username};
        }

        private bool IsSelf(ApiUser user)
        {
            return string.Equals(User.Identity.Name, user.Username, StringComparison.InvariantCultureIgnoreCase);
        }

        private static UserUpdateMode GetUserUpdateMode(UserAttributes attributes)
        {
            return attributes.Overwrite ? UserUpdateMode.Overwrite : UserUpdateMode.NoClobber;
        }
    }
}