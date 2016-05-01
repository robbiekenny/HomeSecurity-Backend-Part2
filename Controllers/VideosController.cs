using Microsoft.Azure.Mobile.Server.Config;
using RestSharp;
using RestSharp.Authenticators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Configuration;
using System.IO;
using homesecurityserviceService.Models;

/*CONTROLLER USED TO RETRIEVE A USERS UPLOADED VIDEOS*/

namespace homesecurityserviceService.Controllers
{
    [MobileAppController]
    [RoutePrefix("videos")]
    public class VideosController : ApiController
    {

        [HttpGet]
        [Route("{email}")]
        public IHttpActionResult Get(string email)
        {
            homesecurityserviceContext db_context = new homesecurityserviceContext();
            var result = db_context.Videos.Where(e => e.Email.Equals(email))
                .Select(e => new
                {
                    Vid = e.Vid,
                    RoomName = e.RoomName,
                    CreatedAt = e.CreatedAt
                }).OrderByDescending(e => e.CreatedAt).ToList();

            if (result.Count > 0)
                return Ok(result);

            return NotFound();
        }
    }
}
