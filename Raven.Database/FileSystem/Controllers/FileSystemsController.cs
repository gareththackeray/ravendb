﻿using Raven.Abstractions.Data;
// -----------------------------------------------------------------------
//  <copyright file="FileSystemsController.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------
using Raven.Abstractions.FileSystem;
using Raven.Database.Extensions;
using Raven.Database.FileSystem.Extensions;
using Raven.Database.Server;
using Raven.Database.Server.Abstractions;
using Raven.Database.Server.Controllers;
using Raven.Database.Server.Security;
using Raven.Database.Server.WebApi.Attributes;
using Raven.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

namespace Raven.Database.FileSystem.Controllers
{
	public class FileSystemsController : RavenDbApiController
	{
		[HttpGet]
		[RavenRoute("fs")]
		public HttpResponseMessage FileSystems(bool getAdditionalData = false)
		{
			HttpResponseMessage responseMessage;

			if (getAdditionalData)
			{
				var data = GetFileSystemsData();
				responseMessage = GetMessageWithObject(data);
			}
			else
			{
				var names = GetFileSystemNames();
				responseMessage = GetMessageWithObject(names);
			}

			return responseMessage.WithNoCache();
		}

        [HttpGet]
        [RavenRoute("fs/status")]
        public HttpResponseMessage Status()
        {
            string status = "ready";
            if (!RavenFileSystem.IsRemoteDifferentialCompressionInstalled)
                status = "install-rdc";

            var result = new { Status = status };

            return GetMessageWithObject(result).WithNoCache();
        }

		[HttpGet]
		[RavenRoute("fs/stats")]
		public async Task<HttpResponseMessage> Stats()
		{
			var stats = new List<FileSystemStats>();

			string[] fileSystemNames = GetFileSystemNames();

			foreach (var fileSystemName in fileSystemNames)
			{
				Task<RavenFileSystem> fsTask;
				if (!FileSystemsLandlord.TryGetFileSystem(fileSystemName, out fsTask)) // we only care about active file systems
					continue;

				if (fsTask.IsCompleted == false)
					continue; // we don't care about in process of starting file systems

				var ravenFileSystem = await fsTask;
				var fsStats = ravenFileSystem.GetFileSystemStats();
				stats.Add(fsStats);
			}

            return GetMessageWithObject(stats).WithNoCache();
		}

		private string[] GetFileSystemNames()
		{
			var fileSystemsData = GetFileSystemsData();
			var fileSystemsNames = fileSystemsData.Select(fileSystemData => fileSystemData.Name);
			return fileSystemsNames.ToArray();
		}

		private IEnumerable<FileSystemData> GetFileSystemsData()
		{
			var start = GetStart();
			var nextPageStart = start; // will trigger rapid pagination
            var fileSystems = Database.Documents.GetDocumentsWithIdStartingWith(Constants.FileSystem.Prefix, null, null, start,
										GetPageSize(Database.Configuration.MaxPageSize), CancellationToken.None, ref nextPageStart);

			var fileSystemsData = fileSystems
				.Select(fileSystem =>
				{
                    var bundles = new string[] { };
                    var settings = fileSystem.Value<RavenJObject>("Settings");
                    if (settings != null)
                    {
                        var activeBundles = settings.Value<string>("Raven/ActiveBundles");
                        if (activeBundles != null)
                        {
                            bundles = activeBundles.Split(';');
                        }
                    }
				    return new FileSystemData
				    {
				        Name = fileSystem.Value<RavenJObject>("@metadata").Value<string>("@id").Replace(Constants.FileSystem.Prefix, string.Empty),
				        Disabled = fileSystem.Value<bool>("Disabled"),
				        Bundles = bundles,
						IsAdminCurrentTenant = true,
				    };
				}).ToList();

			var fileSystemsNames = fileSystemsData.Select(fileSystemObject => fileSystemObject.Name).ToArray();

			List<string> approvedFileSystems = null;
			if (DatabasesLandlord.SystemConfiguration.AnonymousUserAccessMode == AnonymousUserAccessMode.None)
			{
				var authorizer = (MixedModeRequestAuthorizer)ControllerContext.Configuration.Properties[typeof(MixedModeRequestAuthorizer)];
				/*HttpResponseMessage authMsg;
				if (authorizer.TryAuthorize(this, out authMsg) == false)
					return authMsg;

				var user = authorizer.GetUser(this);
				if (user == null)
					return authMsg;*/

				var user = User;
				if (user == null)
					return null;

				if (user.IsAdministrator(DatabasesLandlord.SystemConfiguration.AnonymousUserAccessMode) == false)
				{
					approvedFileSystems = authorizer.GetApprovedResources(user, this, fileSystemsNames);
				}

				//TODO: fix this!
				/*fileSystemsData.ForEach(x =>
				{
					var principalWithDatabaseAccess = user as PrincipalWithDatabaseAccess;
					if (principalWithDatabaseAccess != null)
					{
						var isAdminGlobal = principalWithDatabaseAccess.IsAdministrator(
							DatabasesLandlord.SystemConfiguration.AnonymousUserAccessMode);
						x.IsAdminCurrentTenant = isAdminGlobal || principalWithDatabaseAccess.IsAdministrator(Database);
					}
					else
					{
						x.IsAdminCurrentTenant = user.IsAdministrator(x.Name);
					}
				});*/
			}

			if (approvedFileSystems != null)
			{
				fileSystemsData = fileSystemsData.Where(fileSystemData => approvedFileSystems.Contains(fileSystemData.Name)).ToList();
			}

			return fileSystemsData;
		}

		private class FileSystemData : TenantData
		{
		}
	}
}