using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace homesecurityserviceService.Models
{
    /*DATA ASSOCIATED WITH EACH VIDEO*/
    public class Video
    {
        public string Id { get; set; }
        public string Email { get; set; }
        public string Vid { get; set; }
        public string RoomName { get; set; }
        public string CreatedAt { get; set; }
    }
}