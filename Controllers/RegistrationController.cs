using Microsoft.Azure.NotificationHubs;
using PushApi.Common.Models;
using PushApi.Common.Repositories;
using Swashbuckle.Swagger.Annotations;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Http;

namespace PushApi.Register.Controllers
{
    //[Authorize]
    public class RegistrationController : ApiController
    {
        DbRepository _dbRepo;
        NotificationHubRepository _nhRepo;

        public RegistrationController()
        {
            _dbRepo = new DbRepository();
            
        }

        [SwaggerResponse(System.Net.HttpStatusCode.OK, "OK", typeof(IEnumerable<RegistrationDescription>))]
        public async Task<IEnumerable<RegistrationDescription>> GetAsync(string appId)
        {
            AzureNotificationHub hub = await _dbRepo.GetAzureNotificationHubEndpoint(appId);

            if (string.IsNullOrEmpty(hub.Endpoint) && string.IsNullOrEmpty(hub.HubName))
            {
                throw new Exception($"Unable to find an enpoint for appId = '{appId}'");
            }

            _nhRepo = new NotificationHubRepository(hub.Endpoint, hub.HubName);

            var retval = await _nhRepo.Get();

            return retval;
        }

        //[Authorize]

        [SwaggerResponse(System.Net.HttpStatusCode.OK, "RegistrationId", typeof(IEnumerable<string>))]
        public async Task<string> PostAsync([FromBody]PushRegistration registration)
        {
           
            #region input validation
            if (registration == null)
            {
                throw new ArgumentException("Check one or more of your arguments");
            }

            if (string.IsNullOrEmpty(registration.AppId))
            {
                throw new ArgumentException("Please provide a valid AppId");
            }

            if (string.IsNullOrEmpty(registration.DeviceToken))
            {
                throw new ArgumentException("Please provide a valid DeviceToken");
            }

            if (registration.Platform == Platform.none)
            {
                throw new ArgumentException("Platform can only be iOS, Android or Windows");
            }
            #endregion

            string registrationId = null;

            //registration.UserId = "erikja@microsoft.com";
            //registration.UserId = RequestContext.Principal.Identity.Name.ToLower();
            if (registration.Tags == null) registration.Tags = new List<string>();
            registration.Tags.Add($"UserId:{registration.UserId}");

            AzureNotificationHub hub = await _dbRepo.GetAzureNotificationHubEndpoint(registration.AppId);

            if(string.IsNullOrEmpty(hub.Endpoint) && string.IsNullOrEmpty(hub.HubName))
            {
                throw new Exception($"Unable to find an enpoint for appId = '{registration.AppId}'");
            }

            _nhRepo = new NotificationHubRepository(hub.Endpoint, hub.HubName);

            registrationId = await _nhRepo.Upsert(registration);
            registration.RegistrationId = registrationId;

            var success = await _dbRepo.Upsert(registration);

            //await Clean(registration);

            return registrationId;
        }

   

        private async Task Clean(PushRegistration registration)
        {
            var registrationIds = await _dbRepo.GetRegistrationExcept(registration);

            foreach (var registrationId in registrationIds)
            {
                await _dbRepo.Delete(registrationId);
                await _nhRepo.Delete(registrationId);
            }
        }
    }
}
